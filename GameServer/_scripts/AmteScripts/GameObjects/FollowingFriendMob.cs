using AmteScripts.Areas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Timers;
using AmteScripts.Managers;
using Discord;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Geometry;
using DOLVector = DOL.GS.Geometry.Vector;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.MobGroups;
using DOLDatabase.Tables;
using static DOL.GS.GameObject;

namespace DOL.GS.Scripts
{
    public class FollowingFriendMob : AmteMob, ITextNPC, IFollowingMob
    {
        public const int FOLLOW_MIN_DISTANCE = 100;
        public const int FOLLOW_MAX_DISTANCE = 3000;

        public string MobID { get; set; }
        public string MobName { get; set; }
        public TextNPCPolicy TextNPCIdle { get; set; }
        public TextNPCPolicy TextNPCFollowing { get; set; }
        public Dictionary<string, string> ResponsesFollow { get; set; }
        public Dictionary<string, string> ResponsesUnfollow { get; set; }
        public ushort FollowingFromRadius { get; set; }
        public float AggroMultiplier { get; set; }
        public string LinkedGroupMob { get; set; }
        public string AreaToEnter { get; set; }
        public int TimerBeforeReset { get; set; }
        public bool WaitingInArea { get; set; }
        public bool InFinalSafeAreaJourney { get; set; }
        public string ungroupText;

        public Timer ResetTimer { get; set; }

        private GamePlayer? m_playerFollow;
        
        public GamePlayer? PlayerFollow
        {
            get => m_playerFollow;
            set
            {
                if (m_playerFollow != null)
                    m_playerFollow.FollowingFriendCount--;
                m_playerFollow = value;
                if (value != null)
                    value.FollowingFriendCount++;
            }
        }

        public TextNPCPolicy GetTextNPCPolicy(GameLiving target = null)
        {
            return PlayerFollow == target ? TextNPCFollowing : TextNPCIdle;
        }

        public TextNPCPolicy GetOrCreateTextNPCPolicy(GameLiving target = null)
        {
            if (PlayerFollow == target)
            {
                return TextNPCFollowing ??= new TextNPCPolicy(this);
            }
            else
            {
                return TextNPCIdle ??= new TextNPCPolicy(this);
            }
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player) && (IsPeaceful || WaitingInArea ||
            (((StandardMobBrain)Brain).AggroLevel == 0 && ((StandardMobBrain)Brain).AggroRange == 0))
            && CurrentRegion.GetAreasOfSpot(Coordinate).OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null && a.DbArea.ObjectId == AreaToEnter) != null)
                return false;

            if (PlayerFollow != null)
            {
                if (PlayerFollow != player)
                    return false;
                
                if (TextNPCFollowing == null)
                    return false;

                return TextNPCFollowing.Interact(player);
            }
            
            if (TextNPCIdle == null)
                return false;

            if (WaitingInArea && IsPeaceful)
                return false;

            return TextNPCIdle.Interact(player);
        }
        
        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (WaitingInArea && IsPeaceful)
                return false;

            if (!base.WhisperReceive(source, str))
                return false;
            if (source is not GamePlayer player)
                return false;

            TurnTo(player);
            if (PlayerFollow == player)
            {
                if (TextNPCFollowing != null && TextNPCFollowing.WhisperReceive(player, str) == false)
                    return false;
                string unfollowEntry = null;
                if (str == "ungroup" || (ResponsesUnfollow != null && ResponsesUnfollow.TryGetValue(str.ToLower(), out unfollowEntry)))
                {
                    if (unfollowEntry != null)
                    {
                        string text = string.Format(unfollowEntry, player.Name, player.LastName, player.GuildName, player.Salutation, player.RaceName);
                        player.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    }
                    Notify(GameNPCEvent.FollowLostTarget, this, new FollowLostTargetEventArgs(player));
                    ResetFollow();
                }
            }
            else
            {
                if (TextNPCIdle != null && TextNPCIdle.WhisperReceive(player, str) == false)
                    return false;

                if (ResponsesFollow != null && ResponsesFollow.TryGetValue(str.ToLower(), out var followEntry))
                {
                    if (!ShouldFollow(player, false))
                    {
                        return true;
                    }
                    
                    if (followEntry != null)
                    {
                        string text = string.Format(followEntry, player.Name, player.LastName, player.GuildName, player.Salutation, player.RaceName);
                        player.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    }
                    Follow(player);
                }
            }
            return 
                true;
        }
        
        public bool ShouldFollow(GamePlayer player, bool quiet)
        {
            if (player.IsInPvP)
            {
                var playerList = player.Group?.GetMembers().OfType<GamePlayer>().ToArray() ?? [player];
                var memberCount = playerList.Count();
                var limit = Math.Max(1, 4 - memberCount);
                var numFriends = playerList.Sum(p => p.FollowingFriendCount);
                if (numFriends >= limit)
                {
                    if (!quiet)
                        player.SendTranslatedMessage("TextNPC.TooManyFollowers", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
            }
            return true;
        }

        public override bool AddToWorld()
        {
            WaitingInArea = false;
            if (!base.AddToWorld())
                return false;
            FollowingFriendMobBrain brain = new FollowingFriendMobBrain();
            if (Brain is IOldAggressiveBrain)
            {
                brain.AggroLevel = ((IOldAggressiveBrain)Brain).AggroLevel;
                brain.AggroRange = ((IOldAggressiveBrain)Brain).AggroRange;
            }
            SetOwnBrain(brain);
            return true;
        }

        /// <inheritdoc />
        public override bool RemoveFromWorld()
        {
            PlayerFollow = null;
            return base.RemoveFromWorld();
        }

        #region DB
        public override void LoadFromDatabase(DataObject obj)
        {
            followingfriendmob data = null;
            data = GameServer.Database.SelectObject<followingfriendmob>(t => t.MobID == obj.ObjectId);
            if (data != null)
            {
                MobID = data.MobID;
                var textData = GameServer.Database.FindObjectByKey<DBTextNPC>(data.TextIdle);
                if (textData != null)
                {
                    TextNPCIdle = new TextNPCPolicy(this);
                    TextNPCIdle.LoadFromDatabase(textData);
                }
                textData = GameServer.Database.FindObjectByKey<DBTextNPC>(data.TextFollowing);
                if (textData != null)
                {
                    TextNPCFollowing = new TextNPCPolicy(this);
                    TextNPCFollowing.LoadFromDatabase(textData);
                }
                ResponsesFollow = new Dictionary<string, string>();
                if (data.ReponseFollow != null)
                {
                    foreach (string item in data.ReponseFollow.Split(';'))
                    {
                        string[] items = item.Split('|');
                        if (items.Length != 2)
                            continue;
                        ResponsesFollow.Add(items[0].ToLower(), items[1]);
                    }
                }
                ResponsesUnfollow = new Dictionary<string, string>();
                if (data.ReponseUnfollow != null)
                {
                    foreach (string item in data.ReponseUnfollow.Split(';'))
                    {
                        string[] items = item.Split('|');
                        if (items.Length != 2)
                            continue;
                        ResponsesUnfollow.Add(items[0].ToLower(), items[1]);
                    }
                }
                FollowingFromRadius = data.FollowingFromRadius;
                AggroMultiplier = data.AggroMultiplier;
                LinkedGroupMob = data.LinkedGroupMob;
                AreaToEnter = data.AreaToEnter;
                TimerBeforeReset = data.TimerBeforeReset;
                ResetTimer = new Timer();
                if (TimerBeforeReset == 0)
                    ResetTimer.Interval = 1;
                else
                    ResetTimer.Interval = TimerBeforeReset * 1000;
                ResetTimer.Elapsed += ResetTimer_Elapsed;
            }
            else
            {
                data = new followingfriendmob();
                data.MobID = obj.ObjectId;
                MobID = data.MobID;
                GameServer.Database.AddObject(data);
            }
            base.LoadFromDatabase(obj);
        }
        public override void DeleteFromDatabase()
        {
            followingfriendmob data = null;
            data = GameServer.Database.SelectObject<followingfriendmob>(t => t.MobID == MobID);
            if (data != null)
            {
                var textData = GameServer.Database.FindObjectByKey<DBTextNPC>(data.TextIdle);
                if (textData != null)
                {
                    GameServer.Database.DeleteObject(textData);
                }
                textData = GameServer.Database.FindObjectByKey<DBTextNPC>(data.TextFollowing);
                if (textData != null)
                {
                    GameServer.Database.DeleteObject(textData);
                }
                GameServer.Database.DeleteObject(data);
            }
            base.DeleteFromDatabase();
        }

        private void ResetTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ResetFollow();
        }

        public void ResetFollow()
        {
            if (MobGroups != null)
            {
                foreach (MobGroup group in MobGroups)
                {
                    foreach (FollowingFriendMob mob in group.NPCs.OfType<FollowingFriendMob>())
                    {
                        if (mob.PlayerFollow != null)
                            mob.ResetSelf();
                    }
                }
            }
            
            ResetSelf();
        }

        public void ResetSelf()
        {
            if (WaitingInArea == true && PlayerFollow != null)
            {
                ResetTimer.Stop();
                PlayerFollow.Notify(GameLivingEvent.BringAFriend, PlayerFollow, new BringAFriendArgs(this, false));
            }
            WaitingInArea = false;
            PlayerFollow = null;
            RemoveFromWorld();
            Health = MaxHealth;
            LoadFromDatabase(GameServer.Database.FindObjectByKey<Mob>(InternalID));
            AddToWorld();
        }

        public override void Die(GameObject killer)
        {
            base.Die(killer);
            ResetFollow();
        }
        
        public override void SaveIntoDatabase()
        {
            base.SaveIntoDatabase();
            followingfriendmob data = null;
            MobID = InternalID;
            if (MobID == null)
                return;
            MobName = Name;
            data = GameServer.Database.SelectObject<followingfriendmob>(t => t.MobID == MobID);
            bool isNew = false;
            if (data == null)
            {
                data = new followingfriendmob();
                isNew = true;
            }
            data.MobID = MobID;
            data.ReponseFollow = ResponsesFollow is { Count: > 0 } ? string.Join(";", ResponsesFollow.Select(t => t.Key + "|" + t.Value).ToArray()) : null;
            data.ReponseUnfollow = ResponsesUnfollow is { Count: > 0 } ? string.Join(";", ResponsesUnfollow.Select(t => t.Key + "|" + t.Value).ToArray()) : null;
            data.FollowingFromRadius = FollowingFromRadius;
            data.AggroMultiplier = AggroMultiplier;
            data.LinkedGroupMob = LinkedGroupMob;
            data.AreaToEnter = AreaToEnter;
            data.TimerBeforeReset = TimerBeforeReset;
            if (TextNPCIdle != null)
            {
                TextNPCIdle.SaveIntoDatabase();
                data.TextIdle = TextNPCIdle.TextDB.ObjectId;
            }
            if (TextNPCFollowing != null)
            {
                TextNPCFollowing.SaveIntoDatabase();
                data.TextFollowing = TextNPCFollowing.TextDB.ObjectId;
            }
            if (isNew)
                GameServer.Database.AddObject(data);
            else
                GameServer.Database.SaveObject(data);
        }
        #endregion

        public override void StartAttack(GameObject attackTarget)
        {
            base.StartAttack(attackTarget);
        }

        protected override int FollowTimerCallback(RegionTimer callingTimer)
        {
            if (IsCasting)
                return ServerProperties.Properties.GAMENPC_FOLLOWCHECK_TIME;

            var playerFollow = PlayerFollow;
            if (playerFollow == null)
            {
                Notify(GameNPCEvent.FollowLostTarget, this, new FollowLostTargetEventArgs(null));
                ResetFollow();
                return 0;
            }

            // If target is invalid (dead, different region, etc.), reset
            if (!playerFollow.IsAlive
                || playerFollow.ObjectState != eObjectState.Active
                || CurrentRegionID != playerFollow.CurrentRegionID)
            {
                Notify(GameNPCEvent.FollowLostTarget, this, new FollowLostTargetEventArgs(playerFollow));
                ResetFollow();
                return 0;
            }

            // Decide if we are in "Bring Friends" mode for THIS mob's current zone
            bool isBringFriends = false;
            var currentZone = this.CurrentZone;
            if (PvpManager.Instance != null && PvpManager.Instance.IsOpen && PvpManager.Instance.CurrentSession != null && PvpManager.Instance.CurrentSessionType is PvpManager.eSessionTypes.BringAFriend && currentZone != null)
            {
                // Check if this zone is in the ActiveSession's ZoneList
                var zoneListStr = PvpManager.Instance.CurrentSession.ZoneList;
                if (!string.IsNullOrEmpty(zoneListStr))
                {
                    var zoneStrings = zoneListStr.Split(',');
                    foreach (var zStr in zoneStrings)
                    {
                        if (ushort.TryParse(zStr, out ushort zId))
                        {
                            if (zId == currentZone.ID)
                            {
                                isBringFriends = true;
                                break;
                            }
                        }
                    }
                }
            }

            if (WaitingInArea)
            {
                return 0;
            }

            // 1) If BringFriends => ignore DB area, see if the mob is in the player's safe area
            if (isBringFriends)
            {
                var safeArea = PvpManager.Instance.FindSafeAreaForTarget(playerFollow);
                if (safeArea != null)
                {
                    // Are we physically inside that area boundary already?
                    bool inside = safeArea.IsContaining(Position.Coordinate, false);
                    if (inside)
                    {
                        InFinalSafeAreaJourney = true;
                        StopFollowing();
                        WaitingInArea = true;

                        playerFollow.Notify(GameLivingEvent.BringAFriend, playerFollow, new BringAFriendArgs(this, entered: true, following: false, finalStage: false));

                        const int walkSpeed = 130;

                        var targetPos = safeArea?.Coordinate;
                        if (targetPos == null)
                        {
                            // ???
                        }
                        else
                        {
                            double angle = Math.Atan2(targetPos.Value.Y - Position.Y, targetPos.Value.X - Position.X);
                            targetPos += DOLVector.Create(Angle.Radians(angle), Util.Random(30, 130));

                            WalkTo(targetPos.Value, walkSpeed);
                            return 0;
                        }
                    }
                }
            }
            else
            {
                // 2) Not BringFriends => use old DB-based AreaToEnter
                if (!string.IsNullOrEmpty(AreaToEnter))
                {
                    var currentAreas = CurrentRegion.GetAreasOfSpot(Coordinate);
                    var area = currentAreas?.OfType<AbstractArea>()
                        .FirstOrDefault(a => a.DbArea != null && a.DbArea.ObjectId == AreaToEnter);

                    if (area != null)
                    {
                        StopFollowing();

                        var targetPos = Coordinate.Create(area.DbArea.X, area.DbArea.Y, area.DbArea.Z);
                        var angle = Math.Atan2(targetPos.Y - Position.Y, targetPos.X - Position.X);
                        targetPos += DOLVector.Create(Angle.Radians(angle), Util.Random(100, 250));

                        WaitingInArea = true;
                        playerFollow.Notify(GameLivingEvent.BringAFriend, playerFollow, new BringAFriendArgs(this, entered: true, following: false));

                        WalkTo(targetPos, 130);
                        return 0;
                    }
                }
            }

            // 3) If not arrived => normal follow logic: check distance, approach player
            bool wasInRange = m_followTimer.Properties.getProperty(FOLLOW_TARGET_IN_RANGE, false);
            m_followTimer.Properties.removeProperty(FOLLOW_TARGET_IN_RANGE);

            double distanceToTarget = GetDistanceTo(playerFollow);
            if (distanceToTarget <= FOLLOW_MIN_DISTANCE)
            {
                StopMoving();
                TurnTo(playerFollow);
                if (!wasInRange)
                {
                    m_followTimer.Properties.setProperty(FOLLOW_TARGET_IN_RANGE, true);
                    FollowTargetInRange();
                }
                return ServerProperties.Properties.GAMENPC_FOLLOWCHECK_TIME;
            }
            else if (distanceToTarget > FOLLOW_MAX_DISTANCE)
            {
                Notify(GameNPCEvent.FollowLostTarget, this, new FollowLostTargetEventArgs(playerFollow));
                ResetFollow();
                return 0;
            }

            var diff = playerFollow.Position.Coordinate - Coordinate;
            double distFactor = (double)FOLLOW_MIN_DISTANCE / distanceToTarget;
            var followOffset = diff * distFactor;
            var newPos = playerFollow.Position.Coordinate - followOffset;

            int finalSpeed = MaxSpeed;
            if (distanceToTarget < 300)
                finalSpeed = 0;

            WalkTo(newPos, (short)finalSpeed);
            return ServerProperties.Properties.GAMENPC_FOLLOWCHECK_TIME;
        }

        protected void NotifyPresence()
        {
            //foreach npc in visible distance, notify presence
            foreach (GameNPC npc in GetNPCsInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                npc.Brain?.Notify(GameLivingEvent.TargetInRange, this, new TargetInRangeEventArgs(this));
            }
        }

        public override void _OnArrivedAtTarget()
        {
            base._OnArrivedAtTarget();

            if (InFinalSafeAreaJourney)
            {
                InFinalSafeAreaJourney = false;

                this.Flags |= GameNPC.eFlags.PEACE;
                var owner = PlayerFollow;
                PlayerFollow = null;
                owner.Notify(GameLivingEvent.BringAFriend, owner, new BringAFriendArgs(this, entered: true, following: false, finalStage: true));
            }
        }

        public override IList<string> DelveInfo()
        {
            var info = base.DelveInfo();
            info.Add("");
            info.Add("-- FollowingFriendMob --");
            if (PlayerFollow != null)
            {
                info.Add(" + Mob à suivre: " + PlayerFollow.Name + " (Realm: " + PlayerFollow.Realm + ", Guilde: " + PlayerFollow.Guild.Name + ")");
            }
            else
            {
                info.Add(" + Currently not following any player.");
            }
            return info;
        }

        public override void CustomCopy(GameObject source)
        {
            var followingSource = source as FollowingFriendMob;
            MobName = followingSource!.MobName;
            TextNPCIdle = followingSource.TextNPCIdle;
            TextNPCFollowing = followingSource.TextNPCFollowing;
            ResponsesFollow = followingSource.ResponsesFollow;
            ResponsesUnfollow = followingSource.ResponsesUnfollow;
            FollowingFromRadius = followingSource.FollowingFromRadius;
            AggroMultiplier = followingSource.AggroMultiplier;
            LinkedGroupMob = followingSource.LinkedGroupMob;
            AreaToEnter = followingSource.AreaToEnter;
            TimerBeforeReset = followingSource.TimerBeforeReset;
            ungroupText = followingSource.ungroupText;
            base.CustomCopy(source);
        }

        public void SayRandomPhrase()
        {
            throw new NotImplementedException();
        }

        public void Follow(GameObject obj)
        {
            if (!(Brain is FollowingFriendMobBrain followBrain) || !(obj is GamePlayer player))
            {
                return;
            }
            followBrain.Follow(player);
        }
    }
}

namespace DOL.AI.Brain
{
    public class FollowingFriendMobBrain : AmteMobBrain
    {
        private string FollowMobID => (Body as FollowingMob)?.FollowMobID;

        public override bool Start()
        {
            if (!base.Start())
                return false;
            if (Body is not FollowingFriendMob body)
                return false;
            ScanForPlayers();
            if (body.PlayerFollow != null)
                body.Follow(body.PlayerFollow);
            return true;
        }

        public override void Think()
        {
            FollowingFriendMob body = (FollowingFriendMob)Body;

            //if player quits the game
            if (body.PlayerFollow is { ObjectState: eObjectState.Deleted })
            {
                body.ResetFollow();
                return;
            }
            if (!Body.IsCasting && CheckSpells(eCheckSpellType.Defensive))
                return;

            if (AggroLevel > 0)
            {
                CheckPlayerAggro();
                CheckNPCAggro();
            }

            if (!Body.AttackState && !Body.IsCasting && !Body.IsMoving && Body.Heading != Body.Home.Orientation.InHeading && Body.Position == Body.Home)
            {
                Body.TurnTo(Body.Home.Orientation);
            }

            if (!Body.InCombat)
                Body.TempProperties.removeProperty(GameLiving.LAST_ATTACK_DATA);

            if (Body.CurrentSpellHandler != null || Body.IsMoving || Body.IsMovingOnPath)
                return;
            if (body.PlayerFollow == null)
            {
                if (Body.CurrentFollowTarget == null)
                {
                    ScanForPlayers();
                }
            }
            else if (Body.CurrentFollowTarget != body.PlayerFollow)
            {
                GameLiving followTarget = body.CurrentFollowTarget as GameLiving;
                if (followTarget == null ||
                    followTarget.IsAlive == false ||
                    followTarget.ObjectState != eObjectState.Active || followTarget.CurrentRegionID != Body.CurrentRegionID)
                {
                    body.Follow(body.PlayerFollow);
                }
            }
        }

        private void ScanForPlayers()
        {
            var followingMob = (FollowingFriendMob)Body;
            if (followingMob.FollowingFromRadius > 0 && !followingMob.WaitingInArea)
            {
                var players = Body.GetPlayersInRadius(followingMob.FollowingFromRadius);
                foreach (GamePlayer player in players)
                {
                    if (!followingMob.ShouldFollow(player, true))
                    {
                        continue;
                    }
                    
                    Follow(player);
                    break;
                }
            }
        }

        public void Follow(GamePlayer player)
        {
            ((FollowingFriendMob)Body).PlayerFollow = player;
            player.Notify(GameLivingEvent.BringAFriend, player, new BringAFriendArgs((FollowingFriendMob)Body, true, true));
            Body.Follow(player, FollowingFriendMob.FOLLOW_MIN_DISTANCE, FollowingFriendMob.FOLLOW_MAX_DISTANCE);
        }
    }
}
