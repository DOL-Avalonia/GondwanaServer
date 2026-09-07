using DOL.Database;
using DOL.Events;
using DOL.MobGroups;
using DOL.GS.Behaviour;
using DOL.GS.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.GS.Scripts;
using DOLDatabase.Tables;

namespace DOL.GS.Quests
{
    public class BringAFriendGoal : DataQuestJsonGoal
    {
        private readonly string m_target;
        public override eQuestGoalType Type => eQuestGoalType.Unknown;
        public override int ProgressTotal => m_friendCount;
        private readonly int m_friendCount = 1;
        public override QuestZonePoint PointA { get; }
        private readonly Area.Circle m_area;
        private readonly ushort m_areaRegion;
        public FollowingFriendMob lastFriend;
        bool hasMobGroup = false;

        public BringAFriendGoal(DataQuestJson quest, int goalId, dynamic db) : base(quest, goalId, (object)db)
        {
            m_target = db.TargetName;
            hasInteraction = true;
            GroupMobDb mobGroup = GameServer.Database.SelectObject<GroupMobDb>(t => t.GroupId == m_target);
            if (mobGroup != null)
            {
                hasMobGroup = true;
                int mobGroupCount = GameServer.Database.SelectObjects<GroupMobXMobs>(t => t.GroupId == mobGroup.GroupId).Count();
                m_friendCount = mobGroupCount;
            }
            if (db.AreaRadius != null && db.AreaRadius != "" && db.AreaRegion != null && db.AreaRegion != "" && db.AreaCenter != null)
            {
                m_area = new Area.Circle($"{quest.Name} EnterAreaGoal {goalId}", Coordinate.Create((int)((float)db.AreaCenter.X), (int)((float)db.AreaCenter.Y), (int)((float)db.AreaCenter.Z)), (int)db.AreaRadius);
                m_area.DisplayMessage = false;
                m_areaRegion = db.AreaRegion;

                var reg = WorldMgr.GetRegion(m_areaRegion);
                reg.AddArea(m_area);
                PointA = new QuestZonePoint(reg.GetZone(m_area.Coordinate), m_area.Coordinate);
            }
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("TargetName", m_target);
            dict.Add("AreaCenter", m_area.Coordinate);
            dict.Add("AreaRadius", m_area.Radius);
            dict.Add("AreaRegion", m_areaRegion);
            return dict;
        }

        public override void AbortGoal(PlayerQuest questData)
        {
            if (lastFriend != null)
                lastFriend.ResetFollow();
            base.AbortGoal(questData);
        }

        public override bool EndGoal(PlayerQuest questData, PlayerGoalState goalData, bool force = false)
        {
            if (base.EndGoal(questData, goalData, force))
            {
                if (lastFriend != null)
                    lastFriend.StartResetTimer();
                return true;
            }
            return false;
        }

        public override bool CanInteractWith(PlayerQuest questData, PlayerGoalState state, GameObject target)
            => state?.IsActive == true && target is GameNPC gameNPC && gameNPC.MobGroups?.Any(g => g.GroupId == m_target) == true;

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
            if (e == GameLivingEvent.BringAFriend && args is BringAFriendArgs bringAFriendArgs)
            {
                FollowingFriendMob friend = bringAFriendArgs.Friend as FollowingFriendMob;
                if (friend == null || (friend.Name != m_target && friend.MobGroups?.Any(g => g.GroupId == m_target) != true))
                    return;
                lastFriend = friend;

                if (bringAFriendArgs.Following)
                    return;

                if (bringAFriendArgs.Entered)
                {
                    AdvanceGoal(quest, goal);
                }
                else
                {
                    DecreaseGoal(quest, goal);
                }
            }
        }

        public override void Unload()
        {
            WorldMgr.GetRegion(m_areaRegion)?.RemoveArea(m_area);
            base.Unload();
        }
    }
}
