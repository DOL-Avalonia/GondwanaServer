using DOL.AI.Brain;
using DOL.Database;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&genistar",
        ePrivLevel.Player,
        "Genistar.Command.Help.Desc",
        "Genistar.Command.Help.Care",
        "Genistar.Command.Help.Rename",
        "Genistar.Command.Help.Emote",
        "Genistar.Command.Help.VisibleWeapon",
        "Genistar.Command.Help.Equip",
        "Genistar.Command.Help.RemoveWeapon")]
    [CmdAttribute(
        "&genistar",
        ePrivLevel.GM,
        "Genistar.Command.Help.Exp")]
    public class GenistarCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client?.Player;
            string lang = player!.Client?.Account?.Language ?? "EN";
            if (player == null) return;

            if (args.Length < 2)
            {
                DisplaySyntax(client);
                return;
            }

            string subCmd = args[1].ToLower();

            if (subCmd == "care")
            {
                HandleCareRequest(client, player);
                return;
            }

            GameNPC targetCreature = player.TargetObject as GameNPC;
            DBGenistar dbRecord = null;

            if (targetCreature is GenistarNPC gNpc)
            {
                dbRecord = gNpc.DBRecord;
            }
            else if (targetCreature is GenistarPet gPet)
            {
                dbRecord = gPet.DBRecord;
            }

            if (dbRecord == null || targetCreature == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.TargetVisual"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (subCmd is "exp" or "experience")
            {
                if (client!.Account.PrivLevel < (int)ePrivLevel.GM)
                {
                    DisplaySyntax(client);
                    return;
                }
                HandleExperienceOverrideRequest(player, targetCreature, dbRecord, args);
                return;
            }

            if (dbRecord.OwnerID != player.InternalID && client!.Account.PrivLevel < (int)ePrivLevel.GM)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.RejectsTelemetry"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            switch (subCmd)
            {
                case "rename":
                    HandleRenameRequest(player, targetCreature, dbRecord, args);
                    break;

                case "emote":
                    HandleEmoteRequest(player, targetCreature, args);
                    break;

                case "visibleweapon":
                    HandleVisibleWeaponRequest(player, targetCreature, dbRecord, args);
                    break;

                case "addweapon":
                    HandleAddWeaponRequest(player, targetCreature, dbRecord, args);
                    break;

                case "removeweapon":
                    HandleRemoveWeaponRequest(player, targetCreature, dbRecord, args);
                    break;

                default:
                    DisplaySyntax(client);
                    break;
            }
        }

        private void HandleCareRequest(GameClient client, GamePlayer player)
        {
            string lang = player.Client?.Account?.Language ?? "EN";

            if (player.IsRiding || player.IsMoving || player.InCombat || player.IsStunned || player.IsMezzed)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.PhysicalState"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (player.TempProperties.getProperty<bool>("IsAfkCareMode", false))
            {
                GenistarLensMgr.DisengageCareMode(player, voluntary: true, silent: false);
                return;
            }

            GenistarEgg targetEgg = null;
            foreach (GameNPC npc in player.GetNPCsInRadius(300))
            {
                if (npc is GenistarEgg egg && egg.DBRecord != null && egg.DBRecord.OwnerID == player.InternalID)
                {
                    targetEgg = egg;
                    break;
                }
            }

            if (targetEgg == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.NearIncubator"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            GenistarLensMgr.EngageCareMode(player, targetEgg);
        }

        private void HandleRenameRequest(GamePlayer player, GameNPC creature, DBGenistar db, string[] args)
        {
            string lang = player.Client?.Account?.Language ?? "EN";
            string rawArg = args.Length > 2 ? string.Join(" ", args.Skip(2)) : "";
            bool isReset = string.IsNullOrWhiteSpace(rawArg) || rawArg.Equals("reset", StringComparison.OrdinalIgnoreCase);

            string targetSignature = "";

            if (isReset)
            {
                INpcTemplate tmpl = NpcTemplateMgr.GetTemplate(db.BaseTemplateID);
                targetSignature = tmpl != null ? tmpl.Name : "Genistar";
            }
            else
            {
                targetSignature = rawArg.Trim();

                if (targetSignature.Length > 20)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.NameLimit"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                var invalidNamesMgr = GameServer.Instance?.PlayerManager?.InvalidNames;
                if (invalidNamesMgr != null && invalidNamesMgr[targetSignature])
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.NameRestricted"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    return;
                }
            }

            player.TempProperties.setProperty("GenistarRenameTarget", creature);
            player.TempProperties.setProperty("GenistarRenameString", targetSignature);
            player.TempProperties.setProperty("GenistarRenameIsReset", isReset);

            string promptMessage = isReset
                ? LanguageMgr.GetTranslation(lang, "Genistar.Command.RenameDialogReset", creature.Name, targetSignature)
                : LanguageMgr.GetTranslation(lang, "Genistar.Command.RenameDialog", creature.Name, targetSignature);

            player.Out.SendCustomDialog(promptMessage, new CustomDialogResponse(ConfirmRenameCallback));
        }

        private void ConfirmRenameCallback(GamePlayer player, byte response)
        {
            string lang = player.Client?.Account?.Language ?? "EN";
            GameNPC target = player.TempProperties.getProperty<GameNPC>("GenistarRenameTarget", null);
            string resolvedSignature = player.TempProperties.getProperty<string>("GenistarRenameString", null);
            bool isReset = player.TempProperties.getProperty<bool>("GenistarRenameIsReset", false);

            player.TempProperties.removeProperty("GenistarRenameTarget");
            player.TempProperties.removeProperty("GenistarRenameString");
            player.TempProperties.removeProperty("GenistarRenameIsReset");

            if (response != 1 || target == null || string.IsNullOrWhiteSpace(resolvedSignature)) return;

            DBGenistar db = null;
            if (target is GenistarNPC npc) db = npc.DBRecord;
            else if (target is GenistarPet pet) db = pet.DBRecord;

            if (db == null || db.OwnerID != player.InternalID) return;

            db.CustomName = isReset ? "" : resolvedSignature;
            GameServer.Database.SaveObject(db);
            target.Name = resolvedSignature;

            if (target is GenistarPet gPet && gPet.Brain is IControlledBrain cb)
            {
                cb.UpdatePetWindow();
            }

            string feedback = isReset
                ? LanguageMgr.GetTranslation(lang, "Genistar.Command.RenameResetSuccess", resolvedSignature)
                : LanguageMgr.GetTranslation(lang, "Genistar.Command.RenameSuccess", resolvedSignature);

            player.Out.SendMessage(feedback, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        private void HandleEmoteRequest(GamePlayer player, GameNPC creature, string[] args)
        {
            string lang = player.Client?.Account?.Language ?? "EN";

            if (args.Length < 3)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.EmoteSyntax"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            string rawEmote = args[2].ToLower();
            eEmote emoteID;

            switch (rawEmote)
            {
                case "angry": emoteID = eEmote.Angry; break;
                case "bang": emoteID = eEmote.BangOnShield; break;
                case "beckon": emoteID = eEmote.Beckon; break;
                case "beg": emoteID = eEmote.Beg; break;
                case "bow": emoteID = eEmote.Bow; break;
                case "charge": emoteID = eEmote.LetsGo; break;
                case "cheer": emoteID = eEmote.Cheer; break;
                case "clap": emoteID = eEmote.Clap; break;
                case "confuse": emoteID = eEmote.Confused; break;
                case "cry": emoteID = eEmote.Cry; break;
                case "dance": emoteID = eEmote.Dance; break;
                case "flex": emoteID = eEmote.Flex; break;
                case "kiss": emoteID = eEmote.BlowKiss; break;
                case "laugh": emoteID = eEmote.Laugh; break;
                case "no": emoteID = eEmote.No; break;
                case "point": emoteID = eEmote.Point; break;
                case "ponder": emoteID = eEmote.Ponder; break;
                case "roar": emoteID = eEmote.Roar; break;
                case "salute": emoteID = eEmote.Salute; break;
                case "shrug": emoteID = eEmote.Shrug; break;
                case "wave": emoteID = eEmote.Wave; break;
                case "yes": emoteID = eEmote.Yes; break;
                default:
                    if (!Enum.TryParse<eEmote>(args[2], true, out emoteID))
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.EmoteUnrecognized", args[2]), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }
                    break;
            }

            foreach (GamePlayer plr in creature.GetPlayersInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
            {
                plr.Out.SendEmoteAnimation(creature, emoteID);
            }

            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.EmoteSuccess", creature.Name, rawEmote), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        private void HandleVisibleWeaponRequest(GamePlayer player, GameNPC creature, DBGenistar db, string[] args)
        {
            string lang = player.Client?.Account?.Language ?? "EN";

            if (args.Length < 3)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.VisibleSyntax"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            var category = GenistarEquipmentMgr.GetEquipCategory(db);
            if (category != GenistarEquipmentMgr.eGenistarEquipCategory.Humanoid)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.NoWeaponSwap"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            string slotName = args[2].ToLower();
            byte targetVisibleSlot = 0;

            switch (slotName)
            {
                case "right":
                case "righthand":
                case "left":
                case "lefthand":
                    creature.SwitchWeapon(GameLiving.eActiveWeaponSlot.Standard);
                    targetVisibleSlot = creature.VisibleActiveWeaponSlots;
                    break;
                case "two":
                case "twohand":
                case "twohanded":
                    creature.SwitchWeapon(GameLiving.eActiveWeaponSlot.TwoHanded);
                    targetVisibleSlot = creature.VisibleActiveWeaponSlots;
                    break;
                case "distance":
                case "bow":
                case "ranged":
                    creature.SwitchWeapon(GameLiving.eActiveWeaponSlot.Distance);
                    targetVisibleSlot = creature.VisibleActiveWeaponSlots;
                    break;
                default:
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.VisibleInvalid"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
            }

            db.VisibleWeaponSlot = targetVisibleSlot;
            GameServer.Database.SaveObject(db);

            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.VisibleShifted"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        private void HandleRemoveWeaponRequest(GamePlayer player, GameNPC creature, DBGenistar db, string[] args)
        {
            if (args.Length < 3)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Genistar.Command.RemoveSyntax"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            GenistarEquipmentMgr.TryUnequipGear(player, db, creature, args[2]);
        }

        private void HandleAddWeaponRequest(GamePlayer player, GameNPC creature, DBGenistar db, string[] args)
        {
            string lang = player.Client?.Account?.Language ?? "EN";
            if (args.Length < 3)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.AddSyntax"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!int.TryParse(args[2], out int slotNumber) || slotNumber < 1 || slotNumber > 40)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.AddBounds"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            eInventorySlot slot = (eInventorySlot)(slotNumber + (int)eInventorySlot.FirstBackpack - 1);
            InventoryItem item = player.Inventory.GetItem(slot);

            if (item == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.NoItem"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (GenistarEquipmentMgr.TryEquipGear(player, db, creature, item))
            {
                if (item.Count > 1) player.Inventory.RemoveCountFromStack(item, 1);
                else player.Inventory.RemoveItem(item);
            }
        }

        private void HandleExperienceOverrideRequest(GamePlayer gmPlayer, GameNPC creature, DBGenistar db, string[] args)
        {
            string lang = gmPlayer.Client?.Account?.Language ?? "EN";
            if (args.Length < 4)
            {
                gmPlayer.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.ExpSyntax"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            string mode = args[2].ToLower();
            if (!long.TryParse(args[3], out long modAmount) || modAmount < 0)
            {
                gmPlayer.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.ExpValid"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            byte oldLevel = creature.Level;
            long currentXp = db.GenistarExperience;
            long targetXp = currentXp;

            switch (mode)
            {
                case "add": targetXp += modAmount; break;
                case "sub": targetXp = Math.Max(0, currentXp - modAmount); break;
                case "set": targetXp = modAmount; break;
                default:
                    gmPlayer.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.ExpMode"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
            }

            long delta = targetXp - currentXp;
            db.GenistarExperience = targetXp;
            GameServer.Database.SaveObject(db);

            byte newLevel = creature.Level;

            if (creature is GenistarPet pet)
            {
                if (newLevel != oldLevel)
                {
                    pet.AutoSetStats();
                    if (pet.Brain is IControlledBrain cb) cb.UpdatePetWindow();

                    GamePlayer petOwner = pet.GetLivingOwner() as GamePlayer;
                    petOwner!.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarPet.LevelUp", newLevel), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }
            else if (creature is GenistarNPC npc)
            {
                if (newLevel != oldLevel) npc.AutoSetStats();
            }

            foreach (GamePlayer observer in creature.GetPlayersInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
            {
                bool wasTarget = observer.TargetObject == creature;
                observer.Out.SendObjectRemove(creature);
                observer.Out.SendNPCCreate(creature);

                if (creature.Inventory != null)
                {
                    observer.Out.SendLivingEquipmentUpdate(creature);
                }

                if (wasTarget)
                {
                    observer.Out.SendChangeTarget(creature);
                }
            }

            if (delta > 0 && newLevel > oldLevel)
            {
                new RegionTimer(creature, _ =>
                {
                    foreach (GamePlayer obs in creature.GetPlayersInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
                    {
                        obs.Out.SendSpellEffectAnimation(creature, creature, 8007, 0, false, 1);
                    }
                    return 0;
                }).Start(150);
            }

            gmPlayer.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Command.ExpSuccess", delta, creature.Name, targetXp, creature.Level, creature.Size), eChatType.CT_Staff, eChatLoc.CL_SystemWindow);
        }
    }
}