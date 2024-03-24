using DOL.Database;
using DOL.GS;
using DOL.GS.Commands;
using DOL.MobGroups;
using DOLDatabase.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
          "Commands.GM.GroupMob.Usage.Status.Quest",
          "Commands.GM.GroupMob.Usage.Status.Quest.Flag",
          "Commands.GM.GroupMob.Usage.Status.Quest.Model",
          "Commands.GM.GroupMob.Usage.Status.Quest.Size",
          "Commands.GM.GroupMob.Usage.Status.Quest.Aggro",
          "Commands.GM.GroupMob.Usage.Status.Quest.Range",
          "Commands.GM.GroupMob.Usage.Status.Reset")]

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
                        client.Out.SendMessage($"le groupe {groupId} a été supprimé et les mobs liés à celui-ci enlevés.", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                    }
                    else
                    {
                        client.Out.SendMessage($"Impossible de supprimer le groupe {groupId}", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                    }
                }
                else
                {
                    client.Out.SendMessage("La target doit etre un mob", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
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
                        bool added = MobGroupManager.Instance.AddMobToGroup(target, groupId);
                        if (added)
                        {
                            client.Out.SendMessage($"le mob {target.Name} a été ajouté au groupe {groupId}", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                        }
                        else
                        {
                            client.Out.SendMessage($"Impossible d'ajouter {target.Name} au groupe {groupId}", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                        }

                    }
                    else if (args.Length == 4 && target != null && args[3].ToLowerInvariant() == "spawner")
                    {
                        string spawnerId = target.InternalID;

                        if (!MobGroupManager.Instance.Groups.ContainsKey(groupId))
                        {
                            client.Out.SendMessage($"Le groupe {groupId} n'existe pas.", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                            return;
                        }

                        if (string.IsNullOrEmpty(spawnerId))
                        {
                            client.Out.SendMessage($"Le SpawnderId doit etre défini.", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
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

                        if (!MobGroupManager.Instance.Groups.ContainsKey(spawnKey))
                        {
                            MobGroupManager.Instance.AddMobToGroup(target, spawnKey, false);
                            client.Out.SendMessage($"Le MobGroup du Spawner a été créé avec le GroupId {spawnKey}", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                        }

                        //Add to world will remove mobs from the world (see spawner class)
                        MobGroupManager.Instance.Groups[spawnKey].NPCs.ForEach(n =>
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
                                client.Out.SendMessage($"Le SpawnerTemplate {spawner.MobID} a été correctement sauvegardé", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                            }
                        });
                    }

                    break;


                case "remove":

                    bool removed = MobGroups.MobGroupManager.Instance.RemoveMobFromGroup(target, groupId);
                    if (removed)
                    {
                        client.Out.SendMessage($"le mob {target.Name} a été supprimé du groupe {groupId}", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                    }
                    else
                    {
                        client.Out.SendMessage($"Impossible de supprimer {target.Name} du groupe {groupId}", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                    }
                    break;


                case "info":

                    if (!MobGroupManager.Instance.Groups.ContainsKey(groupId))
                    {
                        client.Out.SendMessage($"Le groupe {groupId} n'existe pas.", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
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
                                client.Out.SendMessage("Le GroupStatusId: " + groupStatusId + " n'existe pas.", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                                return;
                            }

                            MobGroupManager.Instance.Groups[groupId].SetGroupInfo(status, isOriginalStatus: true);
                            MobGroupManager.Instance.Groups[groupId].SaveToDabatase();
                            client.Out.SendMessage("Le GroupStatus: " + groupStatusId + " a été attribué au MobGroup " + groupId, GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
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
                                client.Out.SendMessage("Le SlaveGroupId : " + slaveGroupId + " n'existe pas.", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                                return;
                            }

                            var groupInteract = GameServer.Database.SelectObjects<GroupMobStatusDb>(DB.Column("GroupStatusId").IsEqualTo(groupStatusId))?.FirstOrDefault();

                            if (groupInteract == null)
                            {
                                client.Out.SendMessage("Le GroupStatusId: " + groupStatusId + " n'existe pas.", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
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

                        client.Out.SendMessage("Le GroupStatus a été créé avec le GroupStatusId: " + groupStatus.GroupStatusId, GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
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
                        client.Out.SendMessage(string.Format("Le Group: {0} a été reset{1}", groupId, slave), GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                        return;
                    }

                    break;

                case "quest":
                    if (args.Length == 5)
                    {
                        groupId = args[3];
                        if (!this.isGroupIdAvailable(groupId, client))
                        {
                            break;
                        }
                        if (!ushort.TryParse(args[4], out ushort id))
                        {
                            DisplayMessage(client, "id non correct");
                            break;
                        }
                        switch (args[2].ToLowerInvariant())
                        {
                            case "flag":
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestNPCFlags = id.ToString();
                                break;
                            case "model":
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestNPCModel = id;
                                break;
                            case "size":
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestNPCSize = id;
                                break;
                            case "aggro":
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestAggro = id;
                                break;
                            case "range":
                                MobGroupManager.Instance.Groups[groupId].CompletedQuestRange = id;
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
                        DisplayMessage(client, string.Format("La Quest {0} a bien été associée au GroupMob {1}", questId, groupId));
                    }
                    MobGroupManager.Instance.Groups[groupId].SaveToDabatase();
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
                client.Out.SendMessage("Le GroupId: " + groupId + " n'existe pas.", GS.PacketHandler.eChatType.CT_System, GS.PacketHandler.eChatLoc.CL_ChatWindow);
                return false;
            }

            return true;
        }
    }
}