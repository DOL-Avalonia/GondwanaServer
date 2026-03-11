using DOL.Database;
using DOL.GS;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;
using DOL.GS.Quests;
using DOL.MobGroups;
using DOLDatabase.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DOL.GS.GameNPC;

namespace DOL.commands.gmcommands
{
    [CmdAttribute(
          "&GroupMob",
          ePrivLevel.GM,
          "Commands.GM.GroupMob.Description",
          "Commands.GM.GroupMob.Usage.Add",
          "Commands.GM.GroupMob.Usage.Add.Spawner",
          "Commands.GM.GroupMob.Usage.Remove",
          "Commands.GM.GroupMob.Usage.Group",
          "Commands.GM.GroupMob.Usage.Info",
          "Commands.GM.GroupMob.Usage.Status",
          "Commands.GM.GroupMob.Usage.Status.Origin",
          "Commands.GM.GroupMob.Usage.Status.Create",
          "Commands.GM.GroupMob.Usage.Status.SpellABS",
          "Commands.GM.GroupMob.Usage.Status.MeleeABS",
          "Commands.GM.GroupMob.Usage.Status.DotABS",
          "Commands.GM.GroupMob.Usage.Status.MaxHealth",
          "Commands.GM.GroupMob.Usage.Status.Effectiveness",
          "Commands.GM.GroupMob.Usage.Status.IsFriendly",
          "Commands.GM.GroupMob.Usage.Status.Quest",
          "Commands.GM.GroupMob.Usage.Status.Quest.Flag",
          "Commands.GM.GroupMob.Usage.Status.Quest.Model",
          "Commands.GM.GroupMob.Usage.Status.Quest.Size",
          "Commands.GM.GroupMob.Usage.Status.Quest.Aggro",
          "Commands.GM.GroupMob.Usage.Status.Quest.Range",
          "Commands.GM.GroupMob.Usage.Status.Quest.SpellABS",
          "Commands.GM.GroupMob.Usage.Status.Quest.MeleeABS",
          "Commands.GM.GroupMob.Usage.Status.Quest.DotABS",
          "Commands.GM.GroupMob.Usage.Status.Quest.MaxHealth",
          "Commands.GM.GroupMob.Usage.Status.Quest.Effectiveness",
          "Commands.GM.GroupMob.Usage.Status.Quest.Item",
          "Commands.GM.GroupMob.Usage.Status.Quest.SpellEffect",
          "Commands.GM.GroupMob.Usage.Status.Quest.ItemEffect",
          "Commands.GM.GroupMob.Usage.Status.Reset",
          "Commands.GM.GroupMob.Usage.Assist",
          "Commands.GM.GroupMob.Usage.Save")]

    public class GroupMob
          : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {

            GameNPC target = client.Player.TargetObject as GameNPC;
            string groupId = null;

            if (target == null && args.Length > 3 && args[1].ToLowerInvariant() != "status" && args[1].ToLowerInvariant() != "add" && args[1].ToLowerInvariant() != "quest")
            {
                if (args.Length == 4 && args[1].ToLowerInvariant() == "group" && args[2].ToLowerInvariant() == "remove")
                {
                    groupId = args[3];
                    bool allRemoved = MobGroupManager.Instance.RemoveGroupsAndMobs(groupId);

                    if (allRemoved)
                    {
                        client.Out.SendMessage($"le groupe {groupId} a été supprimé et les mobs liés à celui-ci enlevés.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                    }
                    else
                    {
                        client.Out.SendMessage($"Impossible de supprimer le groupe {groupId}", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                    }
                }
                else
                {
                    client.Out.SendMessage("La target doit etre un mob", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                    this.DisplaySyntax(client);
                }

                return;
            }

            if (args.Length < 3)
            {
                DisplaySyntax(client);
                return;
            }

            groupId = args[2];

            if (string.IsNullOrEmpty(groupId))
            {
                DisplaySyntax(client);
                return;
            }

            switch (args[1].ToLowerInvariant())
            {
                case "add":

                    if (args.Length == 3 && target != null)
                    {
                        bool added = MobGroupManager.Instance.AddMobToGroup(target, groupId) != null;
                        if (added)
                        {
                            client.Out.SendMessage($"le mob {target.Name} a été ajouté au groupe {groupId}", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                        }
                        else
                        {
                            client.Out.SendMessage($"Impossible d'ajouter {target.Name} au groupe {groupId}", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                        }

                    }
                    else if (args.Length == 4 && target != null && args[3].ToLowerInvariant() == "spawner")
                    {
                        string spawnerId = target.InternalID;

                        if (!MobGroupManager.Instance.Groups.ContainsKey(groupId))
                        {
                            client.Out.SendMessage($"Le groupe {groupId} n'existe pas.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            return;
                        }

                        if (string.IsNullOrEmpty(spawnerId))
                        {
                            client.Out.SendMessage($"Le SpawnderId doit etre défini.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            return;
                        }
                        var spawner = GameServer.Database.SelectObjects<SpawnerTemplate>(DB.Column("MobID").IsEqualTo(spawnerId)).FirstOrDefault();

                        if (spawner == null)
                        {
                            spawner = new SpawnerTemplate();
                            spawner.AddsRespawnCount = 0;
                            spawner.IsAggroType = true;
                            spawner.NpcTemplate1 = -1;
                            spawner.NpcTemplate2 = -1;
                            spawner.NpcTemplate3 = -1;
                            spawner.NpcTemplate4 = -1;
                            spawner.NpcTemplate5 = -1;
                            spawner.NpcTemplate6 = -1;
                            spawner.PercentLifeAddsActivity = 0;
                            spawner.MasterGroupId = groupId;
                            spawner.AddsRespawnCount = 0;
                            spawner.AddRespawnTimerSecs = 0;
                            spawner.MobID = spawnerId;
                            GameServer.Database.AddObject(spawner);

                            client.Out.SendMessage($"Le SpawnerTemplate {spawner.MobID} a été sauvegardé", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                        }
                        else
                        {
                            spawner.MasterGroupId = groupId;
                            GameServer.Database.SaveObject(spawner);
                        }

                        string spawnKey = "spwn_" + spawner.ObjectId.Substring(0, 8);

                        MobGroup group;
                        if (!MobGroupManager.Instance.Groups.TryGetValue(spawnKey, out group))
                        {
                            group = MobGroupManager.Instance.AddMobToGroup(target, spawnKey, false);
                            client.Out.SendMessage($"Le MobGroup du Spawner a été créé avec le GroupId {spawnKey}", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                        }

                        //Add to world will remove mobs from the world (see spawner class)
                        group!.NPCs.ForEach(n =>
                        {
                            if (n.InternalID.Equals(spawner.MobID))
                            {
                                var mob = GameServer.Database.FindObjectByKey<Mob>(n.InternalID);

                                if (mob != null)
                                {
                                    n.RemoveFromWorld();
                                    n.LoadFromDatabase(mob);
                                    n.AddToWorld();
                                }
                                client.Out.SendMessage($"Le SpawnerTemplate {spawner.MobID} a été correctement sauvegardé", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            }
                        });
                    }

                    break;


                case "remove":

                    bool removed = MobGroupManager.Instance.RemoveMobFromGroup(target, groupId);
                    if (removed)
                    {
                        client.Out.SendMessage($"le mob {target!.Name} a été supprimé du groupe {groupId}", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                    }
                    else
                    {
                        client.Out.SendMessage($"Impossible de supprimer {target!.Name} du groupe {groupId}", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                    }
                    break;


                case "info":

                    if (!MobGroupManager.Instance.Groups.ContainsKey(groupId))
                    {
                        client.Out.SendMessage($"Le groupe {groupId} n'existe pas.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                        return;
                    }

                    IList<string> infos = MobGroupManager.Instance.GetInfos(MobGroupManager.Instance.Groups[groupId]);

                    if (infos != null)
                    {
                        client.Out.SendCustomTextWindow("[ GROUPMOB " + groupId + " ]", infos);
                    }
                    break;

                case "status":

                    if (args[3].ToLowerInvariant() == "set")
                    {
                        if (args.Length < 6)
                        {
                            DisplaySyntax(client);
                            return;
                        }

                        string groupStatusId = args[4];
                        string slaveGroupId = args[5];

                        if (args[2].ToLowerInvariant() == "origin")
                        {
                            groupId = args[5];

                            if (!this.isGroupIdAvailable(groupId, client))
                            {
                                return;
                            }

                            var status = GameServer.Database.SelectObjects<GroupMobStatusDb>(DB.Column("GroupStatusId").IsEqualTo(groupStatusId))?.FirstOrDefault();

                            if (status == null)
                            {
                                client.Out.SendMessage("Le GroupStatusId: " + groupStatusId + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                                return;
                            }

                            MobGroupManager.Instance.Groups[groupId].SetGroupInfo(status, isOriginalStatus: true);
                            MobGroupManager.Instance.Groups[groupId].SaveToDabatase();
                            client.Out.SendMessage("Le GroupStatus: " + groupStatusId + " a été attribué au MobGroup " + groupId, eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            return;
                        }
                        else
                        {
                            if (!this.isGroupIdAvailable(groupId, client))
                            {
                                return;
                            }

                            if (!MobGroupManager.Instance.Groups.ContainsKey(slaveGroupId))
                            {
                                client.Out.SendMessage("Le SlaveGroupId : " + slaveGroupId + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                                return;
                            }

                            var groupInteract = GameServer.Database.SelectObjects<GroupMobStatusDb>(DB.Column("GroupStatusId").IsEqualTo(groupStatusId))?.FirstOrDefault();

                            if (groupInteract == null)
                            {
                                client.Out.SendMessage("Le GroupStatusId: " + groupStatusId + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                                return;
                            }

                            MobGroupManager.Instance.Groups[groupId].SetGroupInteractions(groupInteract);
                            MobGroupManager.Instance.Groups[groupId].SlaveGroupId = slaveGroupId;
                            MobGroupManager.Instance.Groups[groupId].SaveToDabatase();
                            client.Out.SendMessage("Le MobGroup: " + groupId + " a été associé au GroupMobInteract" + groupInteract.GroupStatusId, GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                            return;
                        }
                    }
                    else if (args[2].ToLowerInvariant() == "create")
                    {
                        if (args.Length != 9)
                        {
                            DisplaySyntax(client);
                            return;
                        }

                        // "'/GroupMob interact <GroupdId> create Effect<SpellId|null> Flag<FlagValue> IsInvicible<true|false|null> Model<id|null> VisibleWeapon<value|null> Race<id|null>'
                        ushort? effect = args[3].ToLowerInvariant() == "null" ? (ushort?)null : ushort.TryParse(args[3], out ushort effectVal) ? effectVal : (ushort?)null;
                        eFlags? flag = args[4].ToLowerInvariant() == "null" ? (eFlags?)null : Enum.TryParse(args[4], out eFlags flagEnum) ? flagEnum : (eFlags?)null;
                        bool? isInvincible = args[5].ToLowerInvariant() == "null" ? (bool?)null : bool.TryParse(args[5], out bool isInvincibleBool) ? isInvincibleBool : (bool?)null;
                        string model = args[6].ToLowerInvariant() == "null" ? null : args[6];
                        byte? visibleWeapon = args[7].ToLowerInvariant() == "null" ? (byte?)null : byte.TryParse(args[7], out byte wp) ? wp : (byte?)null;
                        eRace? race = args[8].ToLowerInvariant() == "null" ? (eRace?)null : Enum.TryParse(args[8], out eRace raceEnum) ? raceEnum : (eRace?)null;

                        var groupStatus = new GroupMobStatusDb();
                        groupStatus.Effect = effect?.ToString();
                        groupStatus.Flag = flag.HasValue ? (int)flag.Value : 0;
                        groupStatus.GroupStatusId = Guid.NewGuid().ToString().Substring(0, 8);
                        groupStatus.Model = model;
                        groupStatus.Race = race?.ToString();
                        groupStatus.SetInvincible = isInvincible?.ToString();
                        groupStatus.VisibleSlot = visibleWeapon?.ToString();

                        try
                        {
                            GameServer.Database.AddObject(groupStatus);
                        }
                        catch
                        {
                            groupStatus.GroupStatusId = Guid.NewGuid().ToString().Substring(0, 8);
                            GameServer.Database.AddObject(groupStatus);
                        }

                        client.Out.SendMessage("Le GroupStatus a été créé avec le GroupStatusId: " + groupStatus.GroupStatusId, eChatType.CT_System, eChatLoc.CL_ChatWindow);
                        return;
                    }
                    else if (args[2].ToLowerInvariant() == "reset")
                    {
                        if (args.Length != 4)
                        {
                            DisplaySyntax(client);
                            return;
                        }

                        groupId = args[3];

                        if (!this.isGroupIdAvailable(groupId, client))
                        {
                            break;
                        }

                        MobGroupManager.Instance.Groups[groupId].ClearGroupInfosAndInterractions();
                        string slave = MobGroupManager.Instance.Groups[groupId].SlaveGroupId != null ? string.Format(" ainsi que son Group Slave: {0}", MobGroupManager.Instance.Groups[groupId].SlaveGroupId) : ".";
                        client.Out.SendMessage(string.Format("Le Group: {0} a été reset{1}", groupId, slave), eChatType.CT_System, eChatLoc.CL_ChatWindow);
                        return;
                    }

                    if (args.Length >= 4)
                    {
                        if (!this.isGroupIdAvailable(groupId, client)) return;

                        if (int.TryParse(args[3], out int value))
                        {
                            string property = args[3].ToLowerInvariant();
                            string valStr = args[4];

                            switch (property)
                            {
                                case "spellabs":
                                    MobGroupManager.Instance.Groups[groupId].GroupInfos.SpellABS = value;
                                    client.Out.SendMessage($"Group {groupId} Status SpellABS set to {value}%", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    break;
                                case "meleeabs":
                                    MobGroupManager.Instance.Groups[groupId].GroupInfos.MeleeABS = value;
                                    client.Out.SendMessage($"Group {groupId} Status MeleeABS set to {value}%", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    break;
                                case "dotabs":
                                    MobGroupManager.Instance.Groups[groupId].GroupInfos.DotABS = value;
                                    client.Out.SendMessage($"Group {groupId} Status DotABS set to {value}%", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    break;
                                case "maxhealth":
                                    MobGroupManager.Instance.Groups[groupId].GroupInfos.MaxHealth = value;
                                    client.Out.SendMessage($"Group {groupId} Status MaxHealth modifier set to {value}%", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    break;
                                case "effectiveness":
                                    MobGroupManager.Instance.Groups[groupId].GroupInfos.Effectiveness = value;
                                    client.Out.SendMessage($"Group {groupId} Status Effectiveness modifier set to {value}%", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    break;
                                case "isfriendly":
                                    if (bool.TryParse(valStr, out bool isFriendly))
                                    {
                                        MobGroupManager.Instance.Groups[groupId].IsQuestConditionFriendly = isFriendly;
                                        client.Out.SendMessage($"Group {groupId} IsQuestConditionFriendly set to {isFriendly}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    }
                                    break;
                                default:
                                    break;
                            }
                            MobGroupManager.Instance.Groups[groupId].ApplyGroupInfos();
                            MobGroupManager.Instance.Groups[groupId].SaveToDabatase();
                        }
                    }
                    break;

                case "quest":
                    if (args.Length == 5)
                    {
                        string action = args[2].ToLowerInvariant();
                        groupId = args[3];
                        string paramValue = args[4];

                        if (!this.isGroupIdAvailable(groupId, client))
                        {
                            break;
                        }

                        ushort id = 0;
                        int val = 0;
                        bool isNumber = ushort.TryParse(paramValue, out id) && int.TryParse(paramValue, out val);

                        switch (action)
                        {
                            case "item":
                                MobGroupManager.Instance.Groups[groupId].EquippedItem = paramValue;
                                client.Out.SendMessage($"Group {groupId} condition item(s) set to: {paramValue}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            case "spelleffect":
                                MobGroupManager.Instance.Groups[groupId].PlayerOnEffectType = paramValue;
                                client.Out.SendMessage($"Group {groupId} condition effect(s) set to: {paramValue}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            case "flag":
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestNPCFlags = paramValue;
                                client.Out.SendMessage($"Group {groupId} condition flag set to: {paramValue}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            case "model":
                                if (!isNumber) { DisplayMessage(client, "Value must be a number"); break; }
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestNPCModel = id;
                                break;
                            case "size":
                                if (!isNumber) { DisplayMessage(client, "Value must be a number"); break; }
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestNPCSize = id;
                                break;
                            case "aggro":
                                if (!isNumber) { DisplayMessage(client, "Value must be a number"); break; }
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestAggro = id;
                                break;
                            case "range":
                                if (!isNumber) { DisplayMessage(client, "Value must be a number"); break; }
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestRange = id;
                                break;
                            case "itemeffect":
                                if (!isNumber) { DisplayMessage(client, "Value must be a number"); break; }
                                MobGroupManager.Instance.Groups[groupId].EquippedItemClientEffect = id;
                                client.Out.SendMessage($"Client Effect when item equipped set to {id}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            case "spellabs":
                                if (!isNumber) { DisplayMessage(client, "Value must be a number"); break; }
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestSpellABS = val;
                                client.Out.SendMessage($"Set SpellABS reduction to {val}% for Quest on Group {groupId}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            case "meleeabs":
                                if (!isNumber) { DisplayMessage(client, "Value must be a number"); break; }
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestMeleeABS = val;
                                client.Out.SendMessage($"Set MeleeABS reduction to {val}% for Quest on Group {groupId}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            case "dotabs":
                                if (!isNumber) { DisplayMessage(client, "Value must be a number"); break; }
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestDotABS = val;
                                client.Out.SendMessage($"Set DotABS reduction to {val}% for Quest on Group {groupId}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            case "maxhealth":
                                if (!isNumber) { DisplayMessage(client, "Value must be a number"); break; }
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestMaxHealth = val;
                                client.Out.SendMessage($"Set MaxHealth reduction target to {val}% for Quest on Group {groupId}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            case "effectiveness":
                                if (!isNumber) { DisplayMessage(client, "Value must be a number"); break; }
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestEffectiveness = val;
                                client.Out.SendMessage($"Set Effectiveness reduction target to {val}% for Quest on Group {groupId}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;

                            default:
                                DisplaySyntax(client);
                                break;
                        }
                    }
                    else
                    {
                        if (args.Length != 6 && args.Length != 7)
                        {
                            DisplaySyntax(client);
                            break;
                        }

                        if (!int.TryParse(args[3], out int questId) || questId <= 0)
                        {
                            DisplayMessage(client, "QuestId non correct");
                            break;
                        }

                        if (!int.TryParse(args[4], out int questCount) || questCount <= 0)
                        {
                            DisplayMessage(client, "QuestCount non correct");
                            break;
                        }

                        if (!bool.TryParse(args[5], out bool isFriendly))
                        {
                            DisplayMessage(client, "isFriendly <true|false> non correct");
                            break;
                        }

                        if (!this.isGroupIdAvailable(groupId, client))
                        {
                            return;
                        }

                        MobGroupManager.Instance.Groups[groupId].CompletedQuestID = questId;
                        MobGroupManager.Instance.Groups[groupId].CompletedQuestCount = questCount;
                        MobGroupManager.Instance.Groups[groupId].IsQuestConditionFriendly = isFriendly;
                        if (args.Length == 7)
                        {
                            if (!ushort.TryParse(args[6], out ushort questStep))
                            {
                                DisplayMessage(client, "QuestStep non correct");
                                break;
                            }
                            MobGroupManager.Instance.Groups[groupId].CompletedStepQuestID = questStep;
                        }
                        var quest = DataQuestJsonMgr.GetQuest((ushort)questId);
                        if (quest == null)
                        {
                            DisplayMessage(client, "La quête {0} n'a pas pu être trouvée", questId);
                        }
                        else
                        {
                            quest.Unload();
                        }
                        DisplayMessage(client, string.Format("La Quest {0} a bien été associée au GroupMob {1}", questId, groupId));
                    }
                    MobGroupManager.Instance.Groups[groupId].SaveToDabatase();
                    break;

                case "assist":
                    {
                        if (args.Length < 4 || !Int32.TryParse(args[3], out int range))
                        {
                            DisplaySyntax(client);
                            break;
                        }

                        MobGroup group;
                        if (!MobGroupManager.Instance.Groups.TryGetValue(groupId, out group) || group == null)
                        {
                            client.SendTranslation("Commands.GM.GroupMob.BadID", eChatType.CT_System, eChatLoc.CL_SystemWindow, groupId);
                            break;
                        }
                        group.AssistRange = range;
                        group.SaveToDabatase();
                        switch (range)
                        {
                            case < 0:
                                client.SendTranslation("Commands.GM.GroupMob.Assist.VisibilityDistance", eChatType.CT_System, eChatLoc.CL_SystemWindow, group.GroupId);
                                break;
                            
                            case 0:
                                client.SendTranslation("Commands.GM.GroupMob.Assist.Never", eChatType.CT_System, eChatLoc.CL_SystemWindow, group.GroupId);
                                break;
                            
                            case > 0:
                                client.SendTranslation("Commands.GM.GroupMob.Assist.RangeSet", eChatType.CT_System, eChatLoc.CL_SystemWindow, group.GroupId, range);
                                break;
                        }
                    }
                    break;

                case "save":
                    if (!this.isGroupIdAvailable(groupId, client)) return;

                    MobGroupManager.Instance.Groups[groupId].SaveToDabatase();
                    client.Out.SendMessage($"GroupMob '{groupId}' has been manually saved to the database.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;

                default:
                    DisplaySyntax(client);
                    break;
            }
        }

        private bool isGroupIdAvailable(string groupId, GameClient client)
        {
            if (!MobGroupManager.Instance.Groups.ContainsKey(groupId))
            {
                client.Out.SendMessage("Le GroupId: " + groupId + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                return false;
            }

            return true;
        }
    }
}