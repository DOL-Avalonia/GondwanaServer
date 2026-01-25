/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using DOL.GS.PacketHandler;
using DOL.Events;
using DOL.Language;
using AmteScripts.Managers;
using DOL.Database;
using System.Threading.Tasks;

namespace DOL.GS
{
    /// <summary>
    /// This class represents a Group inside the game
    /// </summary>
    public class Group
    {
        #region constructor and members
        /// <summary>
        /// Default Constructor with GamePlayer Leader.
        /// </summary>
        /// <param name="leader"></param>
        public Group(GamePlayer leader)
            : this((GameLiving)leader)
        {
        }

        /// <summary>
        /// Default Constructor with GameLiving Leader.
        /// </summary>
        /// <param name="leader"></param>
        public Group(GameLiving leader)
        {
            LivingLeader = leader;
            m_groupMembers = new ReaderWriterList<GameLiving>(ServerProperties.Properties.GROUP_MAX_MEMBER);
        }

        /// <summary>
        /// This holds all players inside the group
        /// </summary>
        protected readonly ReaderWriterList<GameLiving> m_groupMembers;
        #endregion

        #region Leader / Member

        /// <summary>
        /// Gets/sets the group Player leader
        /// </summary>
        public GamePlayer Leader
        {
            get { return LivingLeader as GamePlayer; }
            private set { LivingLeader = value; }
        }

        /// <summary>
        /// Gets/sets the group Living leader
        /// </summary>
        public GameLiving LivingLeader { get; protected set; }

        /// <summary>
        /// Returns the number of players inside this group
        /// </summary>
        public byte MemberCount
        {
            get { return (byte)m_groupMembers.Count; }
        }
        #endregion

        #region mission
        /// <summary>
        /// This Group Mission.
        /// </summary>
        private Quests.AbstractMission m_mission = null;

        /// <summary>
        /// Group Mission
        /// </summary>
        public Quests.AbstractMission Mission
        {
            get { return m_mission; }
            set
            {
                m_mission = value;
                foreach (GamePlayer player in m_groupMembers.OfType<GamePlayer>())
                {
                    player.Out.SendQuestListUpdate();
                    if (value != null)
                        player.Out.SendMessage(m_mission.Description, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
        }
        #endregion

        #region autosplit
        /// <summary>
        /// Gets or sets the group's autosplit loot flag
        /// </summary>
        protected bool m_autosplitLoot = true;

        /// <summary>
        /// Gets or sets the group's autosplit loot flag
        /// </summary>
        public bool AutosplitLoot
        {
            get { return m_autosplitLoot; }
            set { m_autosplitLoot = value; }
        }

        /// <summary>
        /// Gets or sets the group's autosplit coins flag
        /// </summary>
        protected bool m_autosplitCoins = true;

        /// <summary>
        /// Gets or sets the group's autosplit coins flag
        /// </summary>
        public bool AutosplitCoins
        {
            get { return m_autosplitCoins; }
            set { m_autosplitCoins = value; }
        }
        #endregion

        #region lfg status
        /// <summary>
        /// This holds the status of the group
        /// eg. looking for members etc ...
        /// </summary>
        protected byte m_status = 0x0A;

        /// <summary>
        /// Gets or sets the status of this group
        /// </summary>
        public byte Status
        {
            get { return m_status; }
            set { m_status = value; }
        }
        #endregion

        #region managing members
        /// <summary>
        /// Gets all members of the group
        /// </summary>
        /// <returns>Array of GameLiving in this group</returns>
        public ICollection<GameLiving> GetMembersInTheGroup()
        {
            return m_groupMembers.ToArray();
        }

        public ICollection<GamePlayer> GetNearbyPlayersInTheGroup(GamePlayer source)
        {
            return m_groupMembers.OfType<GamePlayer>().Where(groupmate =>
                source.GetDistanceTo(groupmate) <= WorldMgr.MAX_EXPFORKILL_DISTANCE).ToArray();
        }

        /// <summary>
        /// Gets all players of the group
        /// </summary>
        /// <returns>Array of GamePlayers in this group</returns>
        public ICollection<GamePlayer> GetPlayersInTheGroup()
        {
            return m_groupMembers.OfType<GamePlayer>().ToArray();
        }

        public IEnumerable<GameObject> GetMembers()
        {
            return m_groupMembers;
        }

        /// <summary>
        /// Adds a living to the group
        /// </summary>
        /// <param name="living">GameLiving to be added to the group</param>
        /// <returns>true if added successfully</returns>
        public virtual bool AddMember(GameLiving living)
        {
            bool added = m_groupMembers.FreezeWhile<bool>(l =>
            {
                GamePlayer existing = null;
                int count = 0;
                if (living is GamePlayer)
                {
                    foreach (GameLiving member in l)
                    {
                        if (living == member)
                            return false;

                        ++count;
                        if (member is GamePlayer { Client.ClientState: GameClient.eClientState.Linkdead or GameClient.eClientState.Disconnected } player && player.InternalID == living.InternalID)
                        {
                            existing = player;
                            --count;
                        }
                    }
                }
                else
                {
                    if (l.Contains(living))
                        return false;

                    count = l.Count;
                }

                if (count >= ServerProperties.Properties.GROUP_MAX_MEMBER || count >= (byte.MaxValue - 1))
                    return false;

                var index = (byte)(l.Count);
                if (existing is not null)
                {
                    index = existing.GroupIndex;
                    l[existing.GroupIndex] = living;
                    existing.Group = null;
                    existing.GroupIndex = 0xFF;
                }
                else
                {
                    l.Add(living);
                }

                living.Group = this;
                living.GroupIndex = index;
                return true;
            });
            
            if (!added)
                return false;

            // update icons of joined player to everyone in the group
            UpdateGroupWindow();
            UpdateMember(living, true, false);

            if (living is GamePlayer player)
            {
                if (PvpManager.Instance.IsPlayerInQueue(player))
                {
                    PvpManager.Instance.DequeueSolo(player);
                }

                if (player.IsInPvP)
                    PvpManager.Instance.OnMemberJoinGroup(this, player);

                // update all icons for just joined player
                player.Out.SendGroupMembersUpdate(true, true);
                SendTranslatedMessageToGroupMembers("GameUtils.Group.PlayerJoined", eChatType.CT_System, eChatLoc.CL_SystemWindow, player.GetPersonalizedName(player));
            }
            else
                SendTranslatedMessageToGroupMembers("GameUtils.Group.LivingJoined", eChatType.CT_System, eChatLoc.CL_SystemWindow, living.Name);
            GameEventMgr.Notify(GroupEvent.MemberJoined, this, new MemberJoinedEventArgs(living));
            return true;
        }

        public bool MemberDisband(GameLiving who)
        {
            if (who is GamePlayer player && !CheckDisbandAllowed(player))
                return false;

            return RemoveMember(who);
        }

        /// <summary>
        /// Removes a living from the group
        /// </summary>
        /// <param name="living">GameLiving to be removed</param>
        /// <returns>true if removed, false if not</returns>
        public virtual bool RemoveMember(GameLiving living)
        {
            var player = living as GamePlayer;
            
            if (!m_groupMembers.TryRemove(living))
                return false;

            if (MemberCount < 1)
                DisbandGroup();

            living.Group = null;
            living.GroupIndex = 0xFF;

            // Update Player.
            if (player != null)
            {
                player.Out.SendGroupWindowUpdate();
                player.Out.SendQuestListUpdate();
            }

            UpdateGroupWindow();

            if (player != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Group.YouLeft"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                player.Notify(GamePlayerEvent.LeaveGroup, player, new LeaveGroupEventArgs(this));
                SendTranslatedMessageToGroupMembers("GameUtils.Group.PlayerLeft", eChatType.CT_System, eChatLoc.CL_SystemWindow, player.GetPersonalizedName(player));
            }
            else
                SendTranslatedMessageToGroupMembers("GameUtils.Group.LivingLeft", eChatType.CT_System, eChatLoc.CL_SystemWindow, living.Name);

            if (player!.IsInPvP)
                PvpManager.Instance.OnMemberLeaveGroup(this, player);

            // only one member left?
            if (MemberCount == 1)
            {
                // RR4: Group is disbanded, ending mission group if any
                GameLiving remaining = m_groupMembers.First();
                RemoveMember(m_groupMembers.First());

                if (remaining is GamePlayer remainingPlayer)
                {
                    remainingPlayer.Out.SendMessage(LanguageMgr.GetTranslation(remainingPlayer.Client.Account.Language, "GameUtils.Group.DisbandedLastMember"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }

                DisbandGroup();

                if (remaining is GamePlayer gp && PvpManager.Instance.IsOpen && PvpManager.Instance.CurrentSession != null && PvpManager.Instance.CurrentSession.GroupCompoOption == 2)
                {
                    PvpManager.Instance.KickPlayer(gp);
                }

                return true;
            }

            // Update all members
            if (MemberCount > 1 && LivingLeader == living)
            {
                var newLeader = m_groupMembers.OfType<GamePlayer>().First();

                if (newLeader != null)
                {
                    LivingLeader = newLeader;
                    SendTranslatedMessageToGroupMembers("GameUtils.Group.NewLeader", eChatType.CT_System, eChatLoc.CL_SystemWindow, newLeader.GetPersonalizedName(Leader));
                }
                else
                {
                    // Set aother Living Leader.
                    LivingLeader = m_groupMembers.First();
                }
            }

            UpdateGroupIndexes();
            GameEventMgr.Notify(GroupEvent.MemberDisbanded, this, new MemberDisbandedEventArgs(living));

            return true;
        }

        /// <summary>
        /// Clear this group
        /// </summary>
        public void DisbandGroup()
        {
            GroupMgr.RemoveGroup(this);

            if (Mission != null)
                Mission.ExpireMission();

            LivingLeader = null;
            m_groupMembers.Clear();
        }

        /// <summary>
        /// Updates player indexes
        /// </summary>
        private void UpdateGroupIndexes()
        {
            m_groupMembers.FreezeWhile(l =>
            {
                for (byte ind = 0; ind < l.Count; ind++)
                    l[ind].GroupIndex = ind;
            });
        }

        private bool CheckInviteAllowed(GamePlayer player)
        {
            if (!player.IsInPvP || !PvpManager.Instance.IsOpen)
                return true;

            var session = PvpManager.Instance.CurrentSession;
            if (session == null)
                return true;

            if (!session.AllowGroupDisbandCreate)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Group.NoGroupCreationInSession"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            return true;
        }

        private bool CheckDisbandAllowed(GamePlayer player)
        {
            if (!player.IsInPvP || !PvpManager.Instance.IsOpen)
                return true;

            var session = PvpManager.Instance.CurrentSession;
            if (session == null)
                return true;

            if (!session.AllowGroupDisbandCreate)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Group.NoGroupDisbandInSession"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Makes living current leader of the group
        /// </summary>
        /// <param name="living"></param>
        /// <returns></returns>
        public bool MakeLeader(GameLiving living)
        {
            bool allOk = m_groupMembers.FreezeWhile<bool>(l =>
            {
                if (!l.Contains(living))
                    return false;

                byte ind = living.GroupIndex;
                var oldLeader = l[0];
                l[ind] = oldLeader;
                l[0] = living;
                LivingLeader = living;
                living.GroupIndex = 0;
                oldLeader.GroupIndex = ind;

                return true;
            });
            if (allOk)
            {
                // all went ok
                UpdateGroupWindow();
                SendTranslatedMessageToGroupMembers("GameUtils.Group.LeaderChanged", eChatType.CT_System, eChatLoc.CL_SystemWindow, Leader.GetPersonalizedName(Leader));
            }

            return allOk;
        }

        public void OnLinkDeath(GamePlayer player)
        {
            UpdateMember(player, false, false);
        }
        #endregion

        #region messaging
        /// <summary>
        /// Sends a message to all group members with an object from
        /// </summary>
        /// <param name="from">GameLiving source of the message</param>
        /// <param name="msg">message string</param>
        /// <param name="type">message type</param>
        /// <param name="loc">message location</param>
        public virtual void SendMessageToGroupMembers(GameLiving from, string msg, eChatType type, eChatLoc loc)
        {
            AutoTranslator messageTranslator = new AutoTranslator(from as GamePlayer, msg);
            if (from != null)
            {
                foreach (GamePlayer player in GetPlayersInTheGroup())
                {
                    Task.Run(async () =>
                    {
                        string finalMsg = await messageTranslator.Translate(player);

                        player.Out.SendMessage(string.Format("[Party] {0}: \"{1}\"", player.GetPersonalizedName(from), finalMsg), type, loc);
                    });
                }
            }
            else
            {
                foreach (GamePlayer player in GetPlayersInTheGroup())
                {
                    Task.Run(async () =>
                    {
                        string finalMsg = await messageTranslator.Translate(player);

                        player.Out.SendMessage(string.Format("[Party] {0}", finalMsg), type, loc);
                    });
                }
            }
        }

        /// <summary>
        /// Send Raw Message to all group members.
        /// </summary>
        /// <param name="msg">message string</param>
        /// <param name="type">message type</param>
        /// <param name="loc">message location</param>
        public virtual void SendMessageToGroupMembers(string msg, eChatType type, eChatLoc loc)
        {
            foreach (GamePlayer player in GetPlayersInTheGroup())
                player.Out.SendMessage(msg, type, loc);
        }

        /// <summary>
        /// Send Raw Message to all group members.
        /// </summary>
        /// <param name="msg">message string</param>
        /// <param name="type">message type</param>
        /// <param name="loc">message location</param>
        public virtual void SendTranslatedMessageToGroupMembers(string key, eChatType type, eChatLoc loc, params object[] args)
        {
            foreach (GamePlayer player in GetPlayersInTheGroup())
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, key, args), type, loc);
        }

        /// <summary>
        /// Sends a message that a player did an action to all group members
        /// </summary>
        /// <param name="player">the player who did the action</param>
        /// <param name="playerMsg">message to player who did the action</param>
        /// <param name="groupMsg">message to others</param>
        /// <param name="type">message type</param>
        /// <param name="loc">message location</param>
        public void SendPlayerActionToGroupMembers(GamePlayer player, string playerMsg, string groupMsg, PacketHandler.eChatType type, PacketHandler.eChatLoc loc)
        {
            lock (m_groupMembers)
            {
                foreach (GamePlayer pl in m_groupMembers)
                {
                    pl.Out.SendMessage(pl == player ? playerMsg : groupMsg, type, loc);
                }
            }
        }

        /// <summary>
        /// Sends a message that a player did an action to all group members
        /// </summary>
        /// <param name="player">the player who did the action</param>
        /// <param name="groupMsg">message to group members</param>
        /// <param name="type">message type</param>
        /// <param name="loc">message location</param>
        public void SendPlayerActionTranslationToGroupMembers(GamePlayer player, string groupMsg, PacketHandler.eChatType type, PacketHandler.eChatLoc loc, params object[] args)
        {
            lock (m_groupMembers)
            {
                foreach (GamePlayer pl in m_groupMembers)
                {
                    var fullArgs = new object[] { pl.GetPersonalizedName(player) }.Concat(args).ToArray();
                    pl.Out.SendMessage(LanguageMgr.GetTranslation(pl.Client.Account.Language, groupMsg, fullArgs), type, loc);
                }
            }
        }

        /// <summary>
        /// Sends a message that a player did an action to all group members
        /// </summary>
        /// <param name="player">the player who did the action</param>
        /// <param name="playerMsg">message to player who did the action</param>
        /// <param name="groupMsg">message to others</param>
        /// <param name="type">message type</param>
        /// <param name="loc">message location</param>
        public void SendPlayerActionTranslationToGroupMembers(GamePlayer player, string playerMsg, string groupMsg, PacketHandler.eChatType type, PacketHandler.eChatLoc loc, params object[] args)
        {
            lock (m_groupMembers)
            {
                foreach (GamePlayer pl in m_groupMembers)
                {
                    if (pl == player)
                    {
                        pl.Out.SendMessage(LanguageMgr.GetTranslation(pl.Client.Account.Language, playerMsg, args), type, loc);
                    }
                    else
                    {
                        var fullArgs = new object[] { pl.GetPersonalizedName(player) }.Concat(args).ToArray();
                        pl.Out.SendMessage(LanguageMgr.GetTranslation(pl.Client.Account.Language, groupMsg, fullArgs), type, loc);
                    }
                }
            }
        }
        #endregion

        #region update group
        /// <summary>
        /// Updates a group member to all other living in the group
        /// </summary>
        /// <param name="living">living to update</param>
        /// <param name="updateIcons">Do icons need an update</param>
        /// <param name="updateOtherRegions">Should updates be sent to players in other regions</param>
        public void UpdateMember(GameLiving living, bool updateIcons, bool updateOtherRegions)
        {
            if (living.Group != this)
                return;

            foreach (var player in GetPlayersInTheGroup())
            {
                if (updateOtherRegions || player.CurrentRegion == living.CurrentRegion)
                    player.Out.SendGroupMemberUpdate(updateIcons, true, living);
            }
        }

        /// <summary>
        /// Updates all group members to one member
        /// </summary>
        /// <param name="player">The player that should receive updates</param>
        /// <param name="updateIcons">Do icons need an update</param>
        /// <param name="updateOtherRegions">Should updates be sent to players in other regions</param>
        public void UpdateAllToMember(GamePlayer player, bool updateIcons, bool updateOtherRegions)
        {
            if (player.Group != this)
                return;

            foreach (GameLiving living in m_groupMembers)
            {
                if (updateOtherRegions || living.CurrentRegion == player.CurrentRegion)
                {
                    player.Out.SendGroupMemberUpdate(updateIcons, true, living);
                }
            }
        }

        /// <summary>
        /// Updates the group window to all players
        /// </summary>
        public void UpdateGroupWindow()
        {
            foreach (GamePlayer player in GetPlayersInTheGroup())
                player.Out.SendGroupWindowUpdate();
        }
        #endregion

        #region utils
        /// <summary>
        /// If at least one player is in combat group is in combat
        /// </summary>
        /// <returns>true if group in combat</returns>
        public bool IsGroupInCombat()
        {
            return m_groupMembers.Any(m => m.InCombat);
        }

        /// <summary>
        /// Checks if a living is inside the group
        /// </summary>
        /// <param name="living">GameLiving to check</param>
        /// <returns>true if the player is in the group</returns>
        public virtual bool IsInTheGroup(GameLiving living)
        {
            return m_groupMembers.Contains(living);
        }
        #endregion

        /// <summary>
        ///  This is NOT to be used outside of Battelgroup code.
        /// </summary>
        /// <param name="player">Input from battlegroups</param>
        /// <returns>A string of group members</returns>
        public string GroupMemberString(GamePlayer player)
        {
            lock (m_groupMembers)
            {
                StringBuilder text = new StringBuilder(64); //create the string builder
                text.Length = 0;
                BattleGroup mybattlegroup = (BattleGroup)player.TempProperties.getProperty<object>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                foreach (GamePlayer plr in m_groupMembers)
                {
                    if (mybattlegroup.IsInTheBattleGroup(plr))
                    {
                        if ((bool)mybattlegroup.Members[plr] == true)
                        {
                            text.Append("<Leader> ");
                        }
                        text.Append("(I)");
                    }
                    text.Append(player.GetPersonalizedName(plr) + " ");
                }
                return text.ToString();
            }
        }

        /// <summary>
        ///  This is NOT to be used outside of Battelgroup code.
        /// </summary>
        /// <param name="player">Input from battlegroups</param>
        /// <returns>A string of group members</returns>
        public string GroupMemberClassString(GamePlayer player)
        {
            lock (m_groupMembers)
            {
                StringBuilder text = new StringBuilder(64); //create the string builder
                text.Length = 0;
                BattleGroup mybattlegroup = (BattleGroup)player.TempProperties.getProperty<object>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                foreach (GamePlayer plr in m_groupMembers)
                {
                    if (mybattlegroup.IsInTheBattleGroup(plr))
                    {
                        if ((bool)mybattlegroup.Members[plr] == true)
                        {
                            text.Append("<Leader> ");
                        }
                    }
                    text.Append("(" + plr.CharacterClass.Name + ")");
                    text.Append(player.GetPersonalizedName(plr) + " ");
                }
                return text.ToString();
            }
        }
    }
}
