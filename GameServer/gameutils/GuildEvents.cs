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

using AmteScripts.Managers;
using System;
using System.Collections;
using System.Threading;
using DOL.Events;
using DOL.GS;
using DOL.GS.Commands;
using log4net;
using DOL.GS.PacketHandler;
using System.Collections.Generic;
using DOL.Language;
using System.Numerics;

namespace DOL.GS
{
    public class GuildPlayerEvent : GamePlayerEvent
    {
        /// <summary>
        /// Constructs a new GamePlayer Event
        /// </summary>
        /// <param name="name">the event name</param>
        protected GuildPlayerEvent(string name) : base(name) { }
    }

    public class GuildEventHandler
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Static Timer for the Timer to check for expired guild buffs and check leave/enter new members
        /// </summary>
        public static readonly int BUFFCHECK_INTERVAL = 60 * 1000; // 1 Minute

        /// <summary>
        /// Static Timer for the Timer to check for expired guild buffs
        /// </summary>
        private static Timer m_timer;

        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.AddHandler(GamePlayerEvent.NextCraftingTierReached, new DOLEventHandler(OnNextCraftingTierReached));
            GameEventMgr.AddHandler(GamePlayerEvent.GainedExperience, new DOLEventHandler(XPGain));
            GameEventMgr.AddHandler(GamePlayerEvent.GainedRealmPoints, new DOLEventHandler(RealmPointsGain));
            GameEventMgr.AddHandler(GamePlayerEvent.GainedBountyPoints, new DOLEventHandler(BountyPointsGain));
            GameEventMgr.AddHandler(GamePlayerEvent.RRLevelUp, new DOLEventHandler(RealmRankUp));
            GameEventMgr.AddHandler(GamePlayerEvent.RLLevelUp, new DOLEventHandler(RealmRankUp));
            GameEventMgr.AddHandler(GamePlayerEvent.LevelUp, new DOLEventHandler(LevelUp));

            // Guild Buff Check
            m_timer = new Timer(new TimerCallback(StartCheck), m_timer, 0, BUFFCHECK_INTERVAL);
        }

        [ScriptUnloadedEvent]
        public static void OnScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(GamePlayerEvent.NextCraftingTierReached, new DOLEventHandler(OnNextCraftingTierReached));
            GameEventMgr.RemoveHandler(GamePlayerEvent.GainedRealmPoints, new DOLEventHandler(RealmPointsGain));
            GameEventMgr.RemoveHandler(GamePlayerEvent.GainedBountyPoints, new DOLEventHandler(BountyPointsGain));
            GameEventMgr.RemoveHandler(GamePlayerEvent.GainedExperience, new DOLEventHandler(XPGain));
            GameEventMgr.RemoveHandler(GamePlayerEvent.RRLevelUp, new DOLEventHandler(RealmRankUp));
            GameEventMgr.RemoveHandler(GamePlayerEvent.RLLevelUp, new DOLEventHandler(RealmRankUp));
            GameEventMgr.RemoveHandler(GamePlayerEvent.LevelUp, new DOLEventHandler(LevelUp));

            if (m_timer != null)
            {
                m_timer.Dispose();
                m_timer = null;
            }
        }

        #region Crafting Tier

        public static void OnNextCraftingTierReached(DOLEvent e, object sender, EventArgs args)
        {
            NextCraftingTierReachedEventArgs cea = args as NextCraftingTierReachedEventArgs;
            GamePlayer player = sender as GamePlayer;

            if (player != null && player.IsEligibleToGiveMeritPoints)
            {

                // skill  700 - 100 merit points
                // skill  800 - 200 merit points
                // skill  900 - 300 merit points
                // skill 1000 - 400 merit points
                if (cea.Points <= 1000 && cea.Points >= 700)
                {
                    int meritpoints = cea.Points - 600;
                    player.GainGuildMeritPoints(meritpoints);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuildEvent.EarnedMeritPoints", meritpoints), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }
        }

        #endregion Crafting Tier

        #region NPC Kill

        public static void MeritForNPCKilled(GamePlayer player, GameNPC npc, int meritPoints)
        {
            player.GainGuildMeritPoints(meritPoints);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuildEvent.EarnedMeritPoints", meritPoints), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        #endregion NPC Kill

        #region LevelUp

        public static void LevelUp(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = sender as GamePlayer;

            if (player != null && player.IsEligibleToGiveMeritPoints)
            {
                // This equation is a rough guess based on Mythics documentation:
                // ... These scale from 6 at level 2 to 253 at level 50.
                int meritPoints = (int)((double)player.Level * (3.0 + ((double)player.Level / 25.0)));
                player.GainGuildMeritPoints(meritPoints);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuildEvent.EarnedMeritPoints", meritPoints), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        #endregion LevelUp

        #region RealmRankUp

        public static void RealmRankUp(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = sender as GamePlayer;

            if (player == null)
                return;

            if (!player.IsEligibleToGiveMeritPoints)
            {
                return;
            }

            GainedRealmPointsEventArgs rpsArgs = args as GainedRealmPointsEventArgs;

            if (player.RealmLevel % 10 == 0)
            {
                int newRR = 0;
                newRR = ((player.RealmLevel / 10) + 1);
                if (player.Guild != null && player.RealmLevel > 45)
                {
                    int a = (int)Math.Pow((3 * (newRR - 1)), 2);
                    player.GainGuildMeritPoints(a);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuildEvent.MeritPointsAward", (int)Math.Pow((3 * (newRR - 1)), 2)), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }
            else if (player.RealmLevel > 60)
            {
                int RRHigh = ((int)Math.Floor(player.RealmLevel * 0.1) + 1);
                int RRLow = (player.RealmLevel % 10);
                if (player.Guild != null)
                {
                    int a = (int)Math.Pow((3 * (RRHigh - 1)), 2);
                    player.GainGuildMeritPoints(a);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuildEvent.MeritPointsAward", (int)Math.Pow((3 * (RRHigh - 1)), 2)), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }
            else
            {
                if (player.RealmLevel > 10)
                {
                    if (player.Guild != null)
                    {
                        int RRHigh = ((int)Math.Floor(player.RealmLevel * 0.1) + 1);
                        int a = (int)Math.Pow((3 * (RRHigh - 1)), 2);
                        player.GainGuildMeritPoints(a);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuildEvent.MeritPointsAward", (int)Math.Pow((3 * (RRHigh - 1)), 2)), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                }
            }

            if (player.Guild != null)
                player.Guild.UpdateGuildWindow();
        }

        #endregion

        #region XP Gain

        public static void XPGain(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = sender as GamePlayer;

            if (player == null || player.Guild == null) return;

            GainedExperienceEventArgs xpArgs = args as GainedExperienceEventArgs;

            if (player.Guild != null && player.Guild.BonusType == Guild.eBonusType.Experience && xpArgs.XPSource == GameLiving.eXPSource.NPC)
            {
                int guildLevel = (int)player.Guild.GuildLevel;
                double guildXPBonus = ServerProperties.Properties.GUILD_BUFF_XP;

                if (guildLevel >= 8 && guildLevel <= 15)
                {
                    guildXPBonus *= 1.5;
                }
                else if (guildLevel > 15)
                {
                    guildXPBonus *= 2.0;
                }

                long bonusXP = (long)Math.Ceiling((double)xpArgs.ExpBase * guildXPBonus / 100);
                player.GainExperience(GameLiving.eXPSource.Other, bonusXP, 0, 0, 0, false, 1);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuildEvent.GuildbonusXP", bonusXP), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                player.Guild.UpdateGuildWindow();
            }
        }

        #endregion

        #region RealmPointsGain

        public static void RealmPointsGain(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = sender as GamePlayer;

            if (player == null) return;

            Guild guild = player.IsInRvR ? player.RealGuild : player.Guild;

            if (guild != null)
            {
                GainedRealmPointsEventArgs rpsArgs = args as GainedRealmPointsEventArgs;

                if (guild.BonusType == Guild.eBonusType.RealmPoints)
                {
                    int guildLevel = (int)player.Guild.GuildLevel;
                    double guildRPBonus = ServerProperties.Properties.GUILD_BUFF_RP;

                    if (guildLevel >= 8 && guildLevel <= 15)
                    {
                        guildRPBonus *= 1.5;
                    }
                    else if (guildLevel > 15)
                    {
                        guildRPBonus *= 2.0;
                    }
                    long oldGuildRealmPoints = guild.RealmPoints;
                    long bonusRealmPoints = (long)Math.Ceiling((double)rpsArgs.RealmPoints * guildRPBonus / 100);

                    player.GainRealmPoints(bonusRealmPoints, false, false, false);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuildEvent.GuildbonusRP", bonusRealmPoints), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

                    if ((oldGuildRealmPoints < 100000000) && (guild.RealmPoints > 100000000))
                    {
                        // Report to Newsmgr
                        string message = guild.Name + " [" + GlobalConstants.RealmToName((eRealm)player.Realm) + "] has reached 100,000,000 Realm Points!";
                        NewsMgr.CreateNews(message, player.Realm, eNewsType.RvRGlobal, false);
                    }

                    guild.UpdateGuildWindow();
                }

            }
        }

        #endregion

        #region Bounty Points Gained

        public static void BountyPointsGain(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = sender as GamePlayer;

            if (player == null) return;

            Guild guild = player.IsInRvR ? player.RealGuild : player.Guild;

            if (guild != null)
            {
                GainedBountyPointsEventArgs bpsArgs = args as GainedBountyPointsEventArgs;
                if (guild.BonusType == Guild.eBonusType.BountyPoints)
                {
                    int guildLevel = (int)player.Guild.GuildLevel;
                    double guildBPBonus = ServerProperties.Properties.GUILD_BUFF_BP;

                    if (guildLevel >= 8 && guildLevel <= 15)
                    {
                        guildBPBonus *= 1.5;
                    }
                    else if (guildLevel > 15)
                    {
                        guildBPBonus *= 2.0;
                    }
                    long bonusBountyPoints = (long)Math.Ceiling((double)bpsArgs.BountyPoints * guildBPBonus / 100);
                    player.GainBountyPoints(bonusBountyPoints, false, false, false);
                    guild.BountyPoints += bonusBountyPoints;
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuildEvent.GuildbonusRP", bonusBountyPoints), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
                guild.UpdateGuildWindow();
            }
        }

        #endregion

        #region Guild Buff GameTimer Check

        public static void StartCheck(object timer)
        {
            Thread th = new Thread(new ThreadStart(StartCheckThread));
            th.Start();
        }

        public static void StartCheckThread()
        {
            foreach (Guild checkGuild in GuildMgr.GetAllGuilds())
            {
                if (checkGuild.BonusType != Guild.eBonusType.None)
                {
                    TimeSpan bonusTime = DateTime.Now.Subtract(checkGuild.BonusStartTime);

                    if (bonusTime.TotalMinutes > ServerProperties.Properties.GUILD_BUFF_DURATION_MINUTES)
                    {
                        checkGuild.BonusType = Guild.eBonusType.None;

                        checkGuild.SaveIntoDatabase();

                        foreach (GamePlayer player in checkGuild.GetListOfOnlineMembers())
                        {
                            string message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GuildEvent.GuildBuffEnd");
                            player.Out.SendMessage(message, eChatType.CT_Guild, eChatLoc.CL_ChatWindow);
                        }
                    }
                }

                // Remove player in the invit dictionnary, they can leave or be expelled
                List<GamePlayer> playertoRemoveFromDic = new List<GamePlayer>();
                checkGuild.Invite_Players.Foreach((memberKeyValue) =>
                {
                    TimeSpan inviteTime = DateTime.Now.Subtract(memberKeyValue.Value);
                    if (inviteTime.TotalMinutes >= ServerProperties.Properties.RECRUITMENT_TIMER_OPTION)
                        playertoRemoveFromDic.Add(memberKeyValue.Key);
                });
                playertoRemoveFromDic.ForEach((member) =>
                {
                    checkGuild.Invite_Players.Remove(member);
                });

                // Remove player in the leaver dictionnary, they can be invited
                playertoRemoveFromDic = new List<GamePlayer>();
                checkGuild.Leave_Players.Foreach((playerKeyValue) =>
                {
                    TimeSpan leaveTime = DateTime.Now.Subtract(playerKeyValue.Value);
                    if (leaveTime.TotalMinutes >= ServerProperties.Properties.RECRUITMENT_TIMER_OPTION)
                        playertoRemoveFromDic.Add(playerKeyValue.Key);
                });
                playertoRemoveFromDic.ForEach((member) =>
                {
                    checkGuild.Leave_Players.Remove(member);
                });
            }
        }

        #endregion
    }

}
