using DOL.Database;
using DOL.Events;
using DOL.GameEvents;
using DOL.GS;
using DOL.GS.GameEvents;
using DOLDatabase.Tables;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DOL.MobGroups
{
    public class MobGroupManager
    {
        private readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private static MobGroupManager instance;

        public static MobGroupManager Instance => instance ?? (instance = new MobGroupManager());

        private MobGroupManager()
        {
            this.Groups = new Dictionary<string, MobGroup>();
            this.GroupsToRemoveOnServerLoad = new List<string>();
        }

        public Dictionary<string, MobGroup> Groups
        {
            get;
        }

        public List<string> GroupsToRemoveOnServerLoad
        {
            get;
        }

        public void HandleNpcDeath(GameNPC npc, GameObject killer)
        {
            if (npc.MobGroups == null)
            {
                return;
            }

            bool? forceRespawn = null;
            int respawnTimer = 0;
            foreach (MobGroup group in npc.MobGroups)
            {
                bool allDead = group.IsAllDead(npc);
                if (allDead)
                {
                    this.Groups.TryGetValue(group.GroupId, out MobGroup value);
                    //Handle interaction if any slave group
                    this.HandleInteraction(group);

                    //Reset GroupInfo
                    group.ResetGroupInfo();

                    //Notify
                    GameEventMgr.Notify(GroupMobEvent.MobGroupDead, group);
                    var mobGroupEvent = GameEventManager.Instance.GetEventsStartedByKillingGroup(group);
                    mobGroupEvent.ForEach(e => GameEventManager.Instance.StartEvent(e, killer as GamePlayer));
                }
                if (group.RespawnTogether)
                {
                    forceRespawn = (forceRespawn ?? true) && allDead;
                    respawnTimer = Math.Max(respawnTimer, group.NPCs.Max(npc => npc.RespawnInterval));
                }
            }
            if (forceRespawn == true)
            {
                npc.MobGroups.Where(g => g.RespawnTogether).SelectMany(g => g.NPCs).ForEach(npc => npc.StartRespawn(respawnTimer));
            }
        }

        private void HandleInteraction(MobGroup master)
        {
            if (master.SlaveGroupId != null && master.GroupInteractions != null && this.Groups.TryGetValue(master.SlaveGroupId, out MobGroup slave))
            {
                slave.GroupInfos = MobGroup.CopyGroupInfo(master.GroupInteractions);
                slave.SaveToDabatase();
                slave.ApplyGroupInfos(slave.GroupId.StartsWith("spwn_add_"));
                slave.HasOriginalStatus = false;
            }
        }

        public List<string> GetInfos(MobGroup mobGroup)
        {
            if (mobGroup == null)
            {
                return null;
            }

            var infos = new List<string>();
            infos.Add(" - GroupId : " + mobGroup.GroupId);
            infos.Add(" - Db Id : " + (mobGroup.InternalId ?? string.Empty));
            infos.Add(" ** GroupInfos ** ");
            infos.Add(" - Effect : " + mobGroup.GroupInfos.Effect);
            if (!mobGroup.GroupInfos.Flag.HasValue)
            {
                infos.Add(" - Flags: -");
            }
            else
            {
                infos.Add(string.Format(" - Flags: {0} (0x{1})", ((GameNPC.eFlags)mobGroup.GroupInfos.Flag.Value).ToString("G"), mobGroup.GroupInfos.Flag.Value.ToString("X")));
            }

            infos.Add(" - IsInvincible : " + (mobGroup.GroupInfos.IsInvincible?.ToString() ?? "-"));
            infos.Add(" - Model : " + (mobGroup.GroupInfos.Model?.ToString() ?? "-"));
            infos.Add(" - Race : " + (mobGroup.GroupInfos.Race?.ToString() ?? "-"));
            infos.Add(" - VisibleSlot : " + (mobGroup.GroupInfos.VisibleSlot?.ToString() ?? "-"));
            infos.Add("");
            infos.Add("MobGroup Origin StatusId: " + (mobGroup?.mobGroupOriginFk ?? "-"));
            infos.Add("MobGroup Interact StatusId: " + (mobGroup?.mobGroupInterfactFk ?? "-"));
            infos.Add(" - SlaveGroupId : " + (mobGroup.SlaveGroupId ?? "-"));
            infos.Add("");
            infos.Add("ON Completed QUEST:");
            infos.Add("CompletedQuestID: " + (mobGroup.CompletedQuestID > 0 ? mobGroup.CompletedQuestID.ToString() : "-"));
            infos.Add("CompletedStepQuestID: " + (mobGroup.CompletedStepQuestID > 0 ? mobGroup.CompletedStepQuestID.ToString() : "-"));
            infos.Add("CompletedQuestCount: " + (mobGroup.CompletedQuestID > 0 ? mobGroup.CompletedQuestCount.ToString() : "-"));
            infos.Add("IsQuestConditionFriendly (will become): " + (mobGroup.CompletedQuestID > 0 ? mobGroup.IsQuestConditionFriendly ? "Friendly" : "Aggressive" : "-"));
            infos.Add("CompletedQuestNPCModel: " + (mobGroup.CompletedQuestNPCModel != 0 ? mobGroup.CompletedQuestNPCModel.ToString() : "-"));
            infos.Add("CompletedQuestNPCFlags: " + (mobGroup.CompletedQuestNPCFlags ?? "-"));
            infos.Add("CompletedQuestNPCSize: " + (mobGroup.CompletedQuestNPCSize != 0 ? mobGroup.CompletedQuestNPCSize.ToString() : "-"));
            infos.Add("CompletedQuestAggro: " + (mobGroup.CompletedQuestAggro != 0 ? mobGroup.CompletedQuestAggro.ToString() : "-"));
            infos.Add("CompletedQuestRange: " + (mobGroup.CompletedQuestRange != 0 ? mobGroup.CompletedQuestRange.ToString() : "-"));
            if (mobGroup.GroupInteractions != null)
            {
                infos.Add(" Actions on Group Killed : ");
                infos.Add(" - Set Effect : " + mobGroup.GroupInteractions.Effect);
                infos.Add(" - Set Flag : " + mobGroup.GroupInteractions.Flag?.ToString() ?? "-");
                infos.Add(" - Set IsInvincible : " + (mobGroup.GroupInteractions.IsInvincible?.ToString() ?? "-"));
                infos.Add(" - Set Model : " + (mobGroup.GroupInteractions.Model?.ToString() ?? "-"));
                infos.Add(" - Set Race : " + (mobGroup.GroupInteractions.Race?.ToString() ?? "-"));
                infos.Add(" - Set VisibleSlot : " + (mobGroup.GroupInteractions.VisibleSlot?.ToString() ?? "-"));
            }
            infos.Add("******************");
            infos.Add(" - NPC Count: " + mobGroup.NPCs.Count);
            mobGroup.NPCs.ForEach(n => infos.Add(string.Format("Name: {0} | Id: {1} | Region: {2} | Alive: {3} ", n.Name, n.ObjectID, n.CurrentRegionID, n.IsAlive)));
            return infos;
        }

        public string GetGroupIdFromMobId(string mobId)
        {
            if (mobId == null)
            {
                return null;
            }

            foreach (var group in this.Groups)
            {
                if (group.Value.NPCs.Any(npc => npc.InternalID.Equals(mobId)))
                {
                    return group.Key;
                }
            }

            return null;
        }

        public bool RemoveGroupsAndMobs(string groupId, bool isLoadedFromScript = false)
        {
            if (!this.Groups.Remove(groupId, out MobGroup group))
            {
                return false;
            }
            
            group.RemoveAllMobs(isLoadedFromScript);
            
            var db = GameServer.Database.SelectObjects<GroupMobDb>(DB.Column("GroupId").IsEqualTo(groupId))?.FirstOrDefault();
            if (db != null)
            {
                GameServer.Database.DeleteObject(db);
            }

            return true;
        }


        public bool LoadFromDatabase()
        {
            var groups = GameServer.Database.SelectAllObjects<GroupMobXMobs>();

            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (!this.Groups.ContainsKey(group.GroupId))
                    {
                        var groupDb = GameServer.Database.SelectObjects<GroupMobDb>(DB.Column("GroupId").IsEqualTo(group.GroupId))?.FirstOrDefault();
                        if (groupDb != null)
                        {
                            var groupInteraction = groupDb.GroupMobInteract_FK_Id != null ?
                                                    GameServer.Database.SelectObjects<GroupMobStatusDb>(DB.Column("GroupStatusId").IsEqualTo(groupDb.GroupMobInteract_FK_Id))?.FirstOrDefault() : null;
                            var originalStatus = groupDb.GroupMobOrigin_FK_Id != null ?
                                                    GameServer.Database.SelectObjects<GroupMobStatusDb>(DB.Column("GroupStatusId").IsEqualTo(groupDb.GroupMobOrigin_FK_Id))?.FirstOrDefault() : null;
                            this.Groups.Add(group.GroupId, new MobGroup(groupDb, groupInteraction, originalStatus));
                        }
                    }

                    if (WorldMgr.Regions.ContainsKey(group.RegionID))
                    {
                        var mobInWorld = WorldMgr.Regions[group.RegionID].Objects?.FirstOrDefault(o => o?.InternalID?.Equals(group.MobID) == true && o is GameNPC) as GameNPC;

                        if (mobInWorld != null && this.Groups.TryGetValue(group.GroupId, out MobGroup mobGroup))
                        {
                            if (!mobGroup.NPCs.Any(m => m.InternalID.Equals(mobInWorld.InternalID)))
                            {
                                mobGroup.AddMob(mobInWorld);
                                mobInWorld.AddToMobGroup(mobGroup);
                            }
                        }
                    }
                }
                Instance.Groups.Foreach(g => g.Value.ApplyGroupInfos());
            }

            //remove npc from spawner interractions
            foreach (var groupId in this.GroupsToRemoveOnServerLoad)
            {
                if (this.Groups.TryGetValue(groupId, out MobGroup group))
                {
                    foreach (var npc in group.NPCs)
                    {
                        npc.RemoveFromWorld();
                        npc.Delete();
                    }
                }
            }

            return true;
        }

        public MobGroup? AddMobToGroup(GameNPC npc, MobGroup mobGroup, bool isLoadedFromScript = false)
        {
            if (npc == null || mobGroup == null)
            {
                return null;
            }

            if (mobGroup.NPCs.Contains(npc))
            {
                npc.AddToMobGroup(mobGroup);
                return mobGroup;
            }

            mobGroup.AddMob(npc);
            npc.AddToMobGroup(mobGroup);
            
            if (!isLoadedFromScript)
            {
                var exists = GameServer.Database.SelectObjects<GroupMobXMobs>(g => g.MobID == npc.InternalID && g.GroupId == mobGroup.GroupId)?.FirstOrDefault();
                if (exists != null)
                {
                    exists.RegionID = npc.CurrentRegionID;
                    exists.GroupId = mobGroup.GroupId;
                    GameServer.Database.SaveObject(exists);
                }
                else
                {
                    GroupMobXMobs newgroup = new GroupMobXMobs()
                    {
                        MobID = npc.InternalID,
                        GroupId = mobGroup.GroupId,
                        RegionID = npc.CurrentRegionID
                    };

                    GameServer.Database.AddObject(newgroup);
                }
            }
            return mobGroup;
        }
        
        public MobGroup? AddMobToGroup(GameNPC npc, string groupId, bool isLoadedFromScript = false)
        {
            if (npc == null || groupId == null)
            {
                return null;
            }

            bool isnew = false;
            MobGroup mobGroup;
            if (!this.Groups.TryGetValue(groupId, out mobGroup))
            {
                mobGroup = new MobGroup(groupId, isLoadedFromScript);
                this.Groups.Add(groupId, mobGroup);

                if (!isLoadedFromScript)
                {
                    var dbGroup = GameServer.Database.SelectObjects<GroupMobDb>(DB.Column("GroupId").IsEqualTo(groupId))?.FirstOrDefault();
                    if (dbGroup == null)
                    {
                        dbGroup = new GroupMobDb() { GroupId = groupId };
                        GameServer.Database.AddObject(dbGroup);
                    }
                    mobGroup.InternalId = dbGroup.ObjectId;
                }
            }
            else if (mobGroup.NPCs.Contains(npc))
            {
                npc.AddToMobGroup(mobGroup);
                return mobGroup;
            }

            mobGroup.AddMob(npc, isLoadedFromScript);
            npc.AddToMobGroup(mobGroup);

            return mobGroup;
        }


        public bool RemoveMobFromGroup(GameNPC npc, string groupId)
        {
            if (npc == null || groupId == null)
            {
                return false;
            }

            if (!this.Groups.TryGetValue(groupId, out MobGroup group))
            {
                log.Error($"Impossible to remove Group because inmemory Groups does not contain groupId: {groupId}");
                return false;
            }
            
            npc.RemoveFromMobGroup(group);
            if (!group.RemoveMob(npc))
            {
                log.Error($"Impossible to remove NPC {npc.InternalID} from groupId: {groupId}");
                return false;
            }
            return true;
        }

        public List<GameNPC> GetMobsBySwitchFamily(string switchFamily)
        {
            List<GameNPC> mobs = new List<GameNPC>();

            foreach (var group in Groups.Values)
            {
                if (group.SwitchFamily == switchFamily)
                {
                    mobs.AddRange(group.NPCs);
                }
            }

            return mobs;
        }
    }
}