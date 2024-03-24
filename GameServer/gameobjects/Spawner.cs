using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS.Styles;
using DOL.MobGroups;
using DOLDatabase.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS
{
    /// <summary>
    /// This Class spanws Mob and add them to a GroupMob
    /// </summary>
    public class Spawner
        : AmteMob
    {
        private readonly string inactiveDefaultGroupStatusAddsKey = "Spawner_inactive_adds";
        private readonly string activeDefaultGroupStatusAddsKey = "Spawner_active_adds";
        public string inactiveGroupStatusAddsKey;
        private string activeGroupStatusAddsKey;
        private string dbId;
        private bool hasLoadedAdd;
        private bool isAddsGroupMasterGroup;
        private string addsGroupmobId;
        private bool isAddsActiveStatus;
        private int percentLifeAddsActivity;
        private bool isAggroType;
        private int npcTemplate1;
        private int npcTemplate2;
        private int npcTemplate3;
        private int npcTemplate4;
        private int npcTemplate5;
        private int npcTemplate6;
        private int addsRespawnCountTotal;
        private int addsRespawnCurrentCount;
        private int addRespawnTimerSecs;
        private DateTime? npcAddsNextPopupTimeStamp;


        public Spawner()
            : base()
        {
        }

        public Spawner(INpcTemplate template)
            : base(template)
        {
        }

        public string SpawnerGroupId
        {
            get
            {
                return "spwn_" + (this.dbId != null ? this.dbId.Substring(0, 8) : Guid.NewGuid().ToString().Substring(0, 8));
            }
        }


        public string InactiveGroupStatusAddsKey
        {
            get
            {
                if (inactiveGroupStatusAddsKey != null)
                {
                    return inactiveGroupStatusAddsKey;
                }

                return inactiveDefaultGroupStatusAddsKey;
            }
        }

        public string ActiveGroupStatusAddsKey
        {
            get
            {
                if (activeGroupStatusAddsKey != null)
                {
                    return activeGroupStatusAddsKey;
                }

                return activeDefaultGroupStatusAddsKey;
            }
        }


        public override void LoadFromDatabase(DataObject obj)
        {
            base.LoadFromDatabase(obj);

            var result = GameServer.Database.SelectObjects<SpawnerTemplate>(DB.Column("MobID").IsEqualTo(obj.ObjectId));
            if (result != null)
            {
                var db = result.FirstOrDefault();

                if (db != null)
                {
                    this.dbId = db.ObjectId;
                    this.percentLifeAddsActivity = db.PercentLifeAddsActivity;
                    this.addRespawnTimerSecs = db.AddRespawnTimerSecs;
                    this.isAggroType = db.IsAggroType;
                    this.npcTemplate1 = db.NpcTemplate1;
                    this.npcTemplate2 = db.NpcTemplate2;
                    this.npcTemplate3 = db.NpcTemplate3;
                    this.npcTemplate4 = db.NpcTemplate4;
                    this.npcTemplate5 = db.NpcTemplate5;
                    this.npcTemplate6 = db.NpcTemplate6;

                    if (db.MasterGroupId != null)
                    {
                        this.npcTemplate1 = -1;
                        this.npcTemplate2 = -1;
                        this.npcTemplate3 = -1;
                        this.npcTemplate4 = -1;
                        this.npcTemplate5 = -1;
                        this.npcTemplate6 = -1;
                        this.isAddsGroupMasterGroup = true;
                        this.addsGroupmobId = db.MasterGroupId;

                        //add Spawner to GroupMob for interractions
                        var spawnerGroup = GameServer.Database.SelectObjects<GroupMobDb>(DB.Column("GroupId").IsEqualTo(this.SpawnerGroupId)).FirstOrDefault();

                        if (spawnerGroup == null)
                        {
                            this.AddSpawnerToMobGroup();
                        }

                        this.UpdateMasterGroupInDatabase();
                    }

                    this.addsRespawnCountTotal = db.AddsRespawnCount;
                    this.addsRespawnCurrentCount = 0;

                    if (db.ActiveStatusId != null)
                        this.activeGroupStatusAddsKey = db.ActiveStatusId;

                    if (db.InactiveStatusId != null)
                        this.inactiveGroupStatusAddsKey = db.InactiveStatusId;
                }
            }
        }

        public override void SaveIntoDatabase()
        {
            base.SaveIntoDatabase();

            var result = GameServer.Database.SelectObjects<SpawnerTemplate>(DB.Column("MobID").IsEqualTo(this.dbId));

            if (result != null)
            {
                var db = result.FirstOrDefault();

                if (db != null)
                {
                    db.IsAggroType = this.isAggroType;
                    db.NpcTemplate1 = this.npcTemplate1;
                    db.NpcTemplate2 = this.npcTemplate2;
                    db.NpcTemplate3 = this.npcTemplate3;
                    db.NpcTemplate4 = this.npcTemplate4;
                    db.NpcTemplate5 = this.npcTemplate5;
                    db.NpcTemplate6 = this.npcTemplate6;
                    db.AddRespawnTimerSecs = this.addRespawnTimerSecs;
                    db.MasterGroupId = this.isAddsGroupMasterGroup ? this.addsGroupmobId : null;
                    db.AddsRespawnCount = this.addsRespawnCountTotal;
                    db.PercentLifeAddsActivity = this.percentLifeAddsActivity;

                    if (!this.ActiveGroupStatusAddsKey.Equals(this.activeDefaultGroupStatusAddsKey))
                    {
                        db.ActiveStatusId = this.ActiveGroupStatusAddsKey;
                    }

                    if (!this.InactiveGroupStatusAddsKey.Equals(this.inactiveDefaultGroupStatusAddsKey))
                    {
                        db.InactiveStatusId = this.InactiveGroupStatusAddsKey;
                    }

                    GameServer.Database.SaveObject(db);
                }
            }
        }


        /// <summary>
        /// Method effective for NpcTemplates pops only
        /// </summary>
        private void ClearNPCTemplatesOldMobs()
        {
            if (this.addsGroupmobId != null && this.hasLoadedAdd && MobGroupManager.Instance.Groups.ContainsKey(this.addsGroupmobId))
            {
                MobGroupManager.Instance.RemoveGroupsAndMobs(this.addsGroupmobId, true);
                this.hasLoadedAdd = false;
            }
        }

        private void UpdateMasterGroupInDatabase()
        {
            var masterGroup = GameServer.Database.SelectObjects<GroupMobDb>(DB.Column("GroupId").IsEqualTo(this.addsGroupmobId)).FirstOrDefault();
            if (masterGroup != null)
            {
                masterGroup.SlaveGroupId = this.SpawnerGroupId;

                //Set default interract if null
                if (masterGroup.GroupMobInteract_FK_Id == null)
                {
                    masterGroup.GroupMobInteract_FK_Id = activeDefaultGroupStatusAddsKey;
                }

                GameServer.Database.SaveObject(masterGroup);
            }
        }


        private void InstanciateMobs()
        {
            GameNPC[] npcs = new GameNPC[6];
            NpcTemplate[] templates = new NpcTemplate[6]
            {
                NpcTemplateMgr.GetTemplate(npcTemplate1),
                NpcTemplateMgr.GetTemplate(npcTemplate2),
                NpcTemplateMgr.GetTemplate(npcTemplate3),
                NpcTemplateMgr.GetTemplate(npcTemplate4),
                NpcTemplateMgr.GetTemplate(npcTemplate5),
                NpcTemplateMgr.GetTemplate(npcTemplate6)
            };

            List<GameNPC> instantiatedNpcs = new List<GameNPC>();

            for (int i = 0; i < templates.Length; i++)
            {
                if (templates[i] != null)
                {
                    GameNPC npc = new GameNPC();
                    npc.LoadTemplate(templates[i]);
                    npc.Name = templates[i].Name;

                    SetCircularPosition(npc, i, templates.Length, WorldMgr.GIVE_ITEM_DISTANCE);
                    instantiatedNpcs.Add(npc);
                }
            }

            AddToMobGroupToNPCTemplates(instantiatedNpcs);
        }

        private void SetCircularPosition(GameNPC npc, int index, int total, float distance)
        {
            double angle = (Math.PI * 2 * index) / total;
            float xOffset = (float)Math.Cos(angle) * distance;
            float yOffset = (float)Math.Sin(angle) * distance;

            npc.Position = new System.Numerics.Vector3(this.Position.X + xOffset, this.Position.Y + yOffset, this.Position.Z);
            npc.Heading = this.Heading;
            npc.RespawnInterval = -1;
            npc.CurrentRegion = WorldMgr.GetRegion(this.CurrentRegionID);
            npc.AddToWorld();
            npc.OwnerID = this.InternalID;
            if (this.Faction != null)
            {
                npc.Faction = FactionMgr.GetFactionByID(this.Faction.ID);
            }
        }

        private void AddSpawnerToMobGroup()
        {
            if (!MobGroupManager.Instance.Groups.ContainsKey(this.SpawnerGroupId))
            {
                MobGroupManager.Instance.AddMobToGroup(this, this.SpawnerGroupId, false);
            }
        }

        private void AddToMobGroupToNPCTemplates(IEnumerable<GameNPC> npcs)
        {
            this.addsGroupmobId = "spwn_add_" + (this.dbId != null ? this.dbId.Substring(0, 8) : Guid.NewGuid().ToString().Substring(0, 8));
            foreach (var npc in npcs)
            {
                MobGroupManager.Instance.AddMobToGroup(npc, this.addsGroupmobId, true);
            }

            GroupMobStatusDb status;

            if (this.percentLifeAddsActivity == 0)
            {
                status = this.GetActiveStatus();
            }
            else
            {
                status = this.GetInativeStatus();
            }

            Task.Run(async () =>
            {
                //Delay animation on mob added to world
                await Task.Delay(500);
                MobGroupManager.Instance.Groups[this.addsGroupmobId].SetGroupInfo(status, true, true);
            });
        }

        public override void StartAttack(GameObject target)
        {
            base.StartAttack(target);

            if (this.isAggroType && !hasLoadedAdd && (npcAddsNextPopupTimeStamp == null || npcAddsNextPopupTimeStamp.Value < DateTime.Now))
            {
                this.LoadAdds();

                if (!isAddsGroupMasterGroup && !isAddsActiveStatus
                    && this.addsGroupmobId != null && MobGroupManager.Instance.Groups.ContainsKey(this.addsGroupmobId))
                {
                    isAddsActiveStatus = true;
                    MobGroupManager.Instance.Groups[this.addsGroupmobId].ResetGroupInfo(true);
                }
            }
        }
        public override void TakeDamage(AttackData ad)
        {
            base.TakeDamage(ad);

            if (!this.isAggroType && IsAlive)
            {
                if (npcAddsNextPopupTimeStamp == null || npcAddsNextPopupTimeStamp.Value < DateTime.Now)
                {
                    if (!this.hasLoadedAdd)
                    {
                        this.LoadAdds();
                    }
                }
            }

            if (addsGroupmobId != null && MobGroupManager.Instance.Groups.ContainsKey(addsGroupmobId))
            {
                if (!isAddsActiveStatus && (this.percentLifeAddsActivity == 0 || this.HealthPercent <= this.percentLifeAddsActivity))
                {
                    this.isAddsActiveStatus = true;
                    if (this.isAddsGroupMasterGroup)
                    {
                        MobGroupManager.Instance.Groups[this.addsGroupmobId].ResetGroupInfo(true);
                    }
                    else
                    {
                        MobGroupManager.Instance.Groups[this.addsGroupmobId].SetGroupInfo(this.GetActiveStatus(), false, true);
                    }
                }
            }
        }


        //Load adds if respawn is passed
        public void LoadAdds()
        {
            if (isAddsGroupMasterGroup)
            {
                if (this.addsGroupmobId != null && MobGroupManager.Instance.Groups.ContainsKey(this.addsGroupmobId))
                {
                    MobGroupManager.Instance.Groups[this.addsGroupmobId].ReloadMobsFromDatabase();
                }
            }
            else
            {
                this.InstanciateMobs();
            }

            this.npcAddsNextPopupTimeStamp = DateTime.Now.AddSeconds(this.addRespawnTimerSecs);
            this.hasLoadedAdd = true;
        }

        public override void WalkToSpawn()
        {
            base.WalkToSpawn();
            this.addsRespawnCurrentCount = 0;
            this.isAddsActiveStatus = false;
            RemoveAdds();
        }
        private void RemoveAdds()
        {
            if (addsGroupmobId != null && MobGroupManager.Instance.Groups.ContainsKey(this.addsGroupmobId))
            {
                MobGroupManager.Instance.Groups[this.addsGroupmobId].NPCs.ForEach(n =>
                {
                    //if npc is spawner it will call this method (see Die)
                    n.Die(this);
                });

                if (!isAddsGroupMasterGroup)
                {
                    this.ClearNPCTemplatesOldMobs();
                }
                else
                {
                    this.hasLoadedAdd = false;
                }
            }
        }

        public void OnGroupMobDead(DOLEvent e, object sender, EventArgs arguments)
        {
            //check group
            MobGroup senderGroup = sender as MobGroup;

            //Check is npc is in combat to allow respawn only in this case
            if (senderGroup != null && senderGroup.GroupId.Equals(this.addsGroupmobId) && this.InCombat)
            {
                //own group is dead
                this.isAddsActiveStatus = false;

                bool respawnValueIsCorrect = false;
                int respawnTimeInMs = addRespawnTimerSecs * 1000;
                if (respawnTimeInMs > 0 && respawnTimeInMs < int.MaxValue)
                {
                    respawnValueIsCorrect = true;
                }

                //Check if group can respawn
                if (this.addsRespawnCountTotal > 0 && this.addsRespawnCurrentCount < this.addsRespawnCountTotal && respawnValueIsCorrect)
                {
                    this.addsRespawnCurrentCount++;
                    foreach (var npc in MobGroupManager.Instance.Groups[this.addsGroupmobId].NPCs)
                    {
                        npc.RespawnInterval = respawnTimeInMs;
                        npc.StartRespawn();
                        npc.RespawnInterval = -1;
                    }
                }
            }
        }

        public override void Die(GameObject killer)
        {
            base.Die(killer);
            this.isAddsActiveStatus = false;
            this.RemoveAdds();
        }


        private GroupMobStatusDb GetInativeStatus()
        {
            var result = GameServer.Database.SelectObjects<GroupMobStatusDb>(DB.Column("GroupStatusId").IsEqualTo(this.InactiveGroupStatusAddsKey));

            if (result != null && result.Any())
            {
                var inactiveStatus = result.FirstOrDefault();
                return inactiveStatus;
            }
            else
            {
                //Default
                bool insertDefault = false;
                var inactiveDefaultList = GameServer.Database.SelectObjects<GroupMobStatusDb>(DB.Column("GroupStatusId").IsEqualTo(this.inactiveDefaultGroupStatusAddsKey));
                GroupMobStatusDb inactiveStatus = null;

                if (inactiveDefaultList != null)
                {
                    var inactiveDefault = inactiveDefaultList.FirstOrDefault();

                    if (inactiveDefault != null)
                    {
                        inactiveStatus = inactiveDefault;
                    }
                    else
                    {
                        insertDefault = true;
                    }
                }
                else
                {
                    insertDefault = true;
                }

                if (insertDefault)
                {
                    eFlags f = eFlags.PEACE | eFlags.CANTTARGET;

                    inactiveStatus = new GroupMobStatusDb()
                    {
                        Flag = (int)f,
                        SetInvincible = true.ToString(),
                        GroupStatusId = this.InactiveGroupStatusAddsKey
                    };
                    GameServer.Database.AddObject(inactiveStatus);
                }

                return inactiveStatus;
            }
        }


        public override bool AddToWorld()
        {
            base.AddToWorld();

            if (this.isAddsGroupMasterGroup && addsGroupmobId != null)
            {
                //remove mastergroup mob if present
                if (MobGroupManager.Instance.Groups.ContainsKey(this.addsGroupmobId))
                {
                    MobGroupManager.Instance.Groups[this.addsGroupmobId].NPCs.ForEach(n =>
                    {
                        n.RemoveFromWorld();
                        n.Delete();
                    });
                }
                else
                {
                    //on server load add groups to remove list
                    MobGroupManager.Instance.GroupsToRemoveOnServerLoad.Add(this.addsGroupmobId);
                }

                this.hasLoadedAdd = false;

                //reset groupinfo
                if (MobGroupManager.Instance.Groups.ContainsKey(this.SpawnerGroupId))
                {
                    MobGroupManager.Instance.Groups[this.SpawnerGroupId].ResetGroupInfo(true);
                }
            }
            else
            {
                //Handle repop by clearing npctemplate pops       
                this.ClearNPCTemplatesOldMobs();
            }

            //reset adds currentCount respawn
            this.addsRespawnCurrentCount = 0;

            //register handler
            GameEventMgr.AddHandler(GameEvents.GroupMobEvent.MobGroupDead, this.OnGroupMobDead);

            return true;
        }


        public override bool RemoveFromWorld()
        {
            GameEventMgr.RemoveHandler(GameEvents.GroupMobEvent.MobGroupDead, this.OnGroupMobDead);
            return base.RemoveFromWorld();
        }


        private GroupMobStatusDb GetActiveStatus()
        {
            var result = GameServer.Database.SelectObjects<GroupMobStatusDb>(DB.Column("GroupStatusId").IsEqualTo(this.ActiveGroupStatusAddsKey));

            if (result != null && result.Any())
            {
                return result.FirstOrDefault();
            }
            else
            {

                GroupMobStatusDb activeStatus = null;

                var activeDefaultList = GameServer.Database.SelectObjects<GroupMobStatusDb>(DB.Column("GroupStatusId").IsEqualTo(this.activeDefaultGroupStatusAddsKey));

                bool insertActive = false;

                if (activeDefaultList != null)
                {
                    var activeDefault = activeDefaultList.FirstOrDefault();

                    if (activeDefault != null)
                    {
                        activeStatus = activeDefault;
                    }
                    else
                    {
                        insertActive = true;
                    }
                }
                else
                {
                    insertActive = true;
                }

                if (insertActive)
                {

                    activeStatus = new GroupMobStatusDb()
                    {
                        SetInvincible = false.ToString(),
                        Flag = 0,
                        GroupStatusId = this.ActiveGroupStatusAddsKey
                    };
                    GameServer.Database.AddObject(activeStatus);
                }

                return activeStatus;
            }
        }
    }
}