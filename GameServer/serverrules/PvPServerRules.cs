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
using System.Collections;
using DOL;
using DOL.Database;
using DOL.Language;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS;
using DOL.GS.Keeps;
using DOL.GS.PacketHandler;
using System.Collections.Generic;
using DOL.GS.ServerProperties;
using DOL.GS.PlayerTitles;

namespace DOL.GS.ServerRules
{
    /// <summary>
    /// Set of rules for "PvP" server type.
    /// </summary>
    //[ServerRules(eGameServerType.GST_PvP)]
    public class PvPServerRules : AbstractServerRules
    {
        public override string RulesDescription()
        {
            return "standard PvP server rules";
        }

        //release city
        //alb=26315, 21177, 8256, dir=0
        //mid=24664, 21402, 8759, dir=0
        //hib=15780, 22727, 7060, dir=0
        //0,"You will now release automatically to your home city in 8 more seconds!"

        //TODO: 2min immunity after release if killed by player

        /// <summary>
        /// TempProperty set if killed by player
        /// </summary>
        protected const string KILLED_BY_PLAYER_PROP = "PvP killed by player";

        public override void ImmunityExpiredCallback(GamePlayer player)
        {
            if (player.ObjectState != GameObject.eObjectState.Active) return;
            if (player.Client.IsPlaying == false) return;

            if (player.Level < m_safetyLevel && player.SafetyFlag)
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ServerRules.PvPServerRules.InvTimerExpFlagON"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            else
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ServerRules.PvPServerRules.InvTimerExp"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            return;
        }

        /// <summary>
        /// Level at which players safety flag has no effect
        /// </summary>
        protected int m_safetyLevel = 10;

        /// <summary>
        /// Invoked on Player death and deals out
        /// experience/realm points if needed
        /// </summary>
        /// <param name="killedPlayer">player that died</param>
        /// <param name="killer">killer</param>
        public override void OnPlayerKilled(GamePlayer killedPlayer, GameObject killer)
        {
            base.OnPlayerKilled(killedPlayer, killer);
            if (killer == null || killer is GamePlayer)
                killedPlayer.TempProperties.setProperty(KILLED_BY_PLAYER_PROP, KILLED_BY_PLAYER_PROP);
            else
                killedPlayer.TempProperties.removeProperty(KILLED_BY_PLAYER_PROP);
        }

        public override void OnReleased(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = (GamePlayer)sender;
            if (player.TempProperties.getProperty<object>(KILLED_BY_PLAYER_PROP, null) != null)
            {
                player.TempProperties.removeProperty(KILLED_BY_PLAYER_PROP);
                StartImmunityTimer(player, ServerProperties.Properties.TIMER_KILLED_BY_PLAYER * 1000);//When Killed by a Player
            }
            else
            {
                StartImmunityTimer(player, ServerProperties.Properties.TIMER_KILLED_BY_MOB * 1000);//When Killed by a Mob
            }
        }


        /// <summary>
        /// Should be called whenever a player teleports to a new location
        /// </summary>
        /// <param name="player"></param>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        public override void OnPlayerTeleport(GamePlayer player, Teleport destination)
        {
            // Since region change already starts an immunity timer we only want to do this if a player
            // is teleporting within the same region
            if (player.CurrentRegionID == destination.RegionID)
            {
                StartImmunityTimer(player, ServerProperties.Properties.TIMER_PVP_TELEPORT * 1000);
            }
        }


        /// <summary>
        /// Regions where players can't be attacked
        /// </summary>
        protected int[] m_safeRegions =
        {
            10,  //City of Camelot
			101, //Jordheim
			201, //Tir Na Nog

			2,   //Albion Housing
			102, //Midgard Housing
			202, //Hibernia Housing

			//No PVP Dungeons: http://support.darkageofcamelot.com/cgi-bin/support.cfg/php/enduser/std_adp.php?p_sid=frxnPUjg&p_lva=&p_refno=020709-000000&p_created=1026248996&p_sp=cF9ncmlkc29ydD0mcF9yb3dfY250PTE0JnBfc2VhcmNoX3RleHQ9JnBfc2VhcmNoX3R5cGU9MyZwX2NhdF9sdmwxPTI2JnBfY2F0X2x2bDI9fmFueX4mcF9zb3J0X2J5PWRmbHQmcF9wYWdlPTE*&p_li
			21,  //Tomb of Mithra
			129, //Nisse's Lair (Nisse's Lair in regions.ini)
			221, //Muire Tomb (Undead in regions.ini)

		};

        /// <summary>
        /// Regions unsafe for players with safety flag
        /// </summary>
        protected int[] m_unsafeRegions =
        {
            163, // new frontiers
		};

        public override bool IsAllowedToAttack(GameLiving attacker, GameLiving defender, bool quiet)
        {
            if (!base.IsAllowedToAttack(attacker, defender, quiet))
                return false;

            // if controlled NPC - do checks for owner instead
            if (attacker is GameNPC)
            {
                var owner = attacker.GetLivingOwner();
                if (owner != null)
                {
                    attacker = owner;
                    quiet = true;
                }
            }
            if (defender is GameNPC)
            {
                defender = defender.GetLivingOwner() ?? defender;
            }

            // can't attack self
            if (attacker == defender)
            {
                if (quiet == false) MessageToLiving(attacker, LanguageMgr.GetTranslation((attacker as GamePlayer)?.Client, "ServerRules.PvPServerRules.AttackSelf"));
                return false;
            }

            //ogre: sometimes other players shouldn't be attackable
            GamePlayer playerAttacker = attacker as GamePlayer;
            GamePlayer playerDefender = defender as GamePlayer;
            if (playerAttacker != null && playerDefender != null)
            {
                //check group
                if (playerAttacker.Group != null && playerAttacker.Group.IsInTheGroup(playerDefender))
                {
                    if (!quiet) MessageToLiving(playerAttacker, LanguageMgr.GetTranslation(playerAttacker.Client, "ServerRules.PvPServerRules.AttackGroupMember"));
                    return false;
                }

                if (playerAttacker.DuelTarget != defender)
                {
                    //check guild
                    if (playerAttacker.Guild != null && playerAttacker.Guild == playerDefender.Guild)
                    {
                        if (!quiet) MessageToLiving(playerAttacker, LanguageMgr.GetTranslation(playerAttacker.Client, "ServerRules.PvPServerRules.AttackGuildMember"));
                        return false;
                    }

                    // Player can't hit other members of the same BattleGroup
                    BattleGroup mybattlegroup = (BattleGroup)playerAttacker.TempProperties.getProperty<object>(BattleGroup.BATTLEGROUP_PROPERTY, null);

                    if (mybattlegroup != null && mybattlegroup.IsInTheBattleGroup(playerDefender))
                    {
                        if (!quiet) MessageToLiving(playerAttacker, LanguageMgr.GetTranslation(playerAttacker.Client, "ServerRules.PvPServerRules.AttackBattleGroupMember"));
                        return false;
                    }

                    // Safe regions
                    if (m_safeRegions != null)
                    {
                        foreach (int reg in m_safeRegions)
                            if (playerAttacker.CurrentRegionID == reg)
                            {
                                if (quiet == false) MessageToLiving(playerAttacker, LanguageMgr.GetTranslation(playerAttacker.Client, "ServerRules.PvPServerRules.SafeZoneNoAttack"));
                                return false;
                            }
                    }


                    // Players with safety flag can not attack other players
                    if (playerAttacker.Level < m_safetyLevel && playerAttacker.SafetyFlag)
                    {
                        if (quiet == false) MessageToLiving(attacker, LanguageMgr.GetTranslation(playerAttacker.Client, "ServerRules.PvPServerRules.SafetyFlagOnSelf"));
                        return false;
                    }

                    // Players with safety flag can not be attacked in safe regions
                    if (playerDefender.Level < m_safetyLevel && playerDefender.SafetyFlag)
                    {
                        bool unsafeRegion = false;
                        foreach (int regionID in m_unsafeRegions)
                        {
                            if (regionID == playerDefender.CurrentRegionID)
                            {
                                unsafeRegion = true;
                                break;
                            }
                        }
                        if (unsafeRegion == false)
                        {
                            if (!quiet)
                                MessageToLiving(attacker,
                                    LanguageMgr.GetTranslation((attacker as GamePlayer)?.Client,
                                        "ServerRules.PvPServerRules.SafetyFlagTargetInSafeArea",
                                        playerDefender.Name,
                                        playerDefender.GetPronoun(1, false),
                                        playerDefender.GetPronoun(2, false)));
                            return false;
                        }
                    }
                }
            }

            if (attacker.Realm == 0 && defender.Realm == 0)
            {
                return FactionMgr.CanLivingAttack(attacker, defender);
            }

            //allow confused mobs to attack same realm
            if (attacker is GameNPC && (attacker as GameNPC)!.IsConfused && attacker.Realm == defender.Realm)
                return true;

            // "friendly" NPCs can't attack "friendly" players
            if (defender is GameNPC && defender.Realm != 0 && attacker.Realm != 0 && defender is GameKeepGuard == false && defender is GameFont == false)
            {
                if (quiet == false) MessageToLiving(attacker, LanguageMgr.GetTranslation((attacker as GamePlayer)?.Client, "ServerRules.PvPServerRules.AttackFriendlyNPC"));
                return false;
            }
            // "friendly" NPCs can't be attacked by "friendly" players
            if (attacker is GameNPC && attacker.Realm != 0 && defender.Realm != 0 && attacker is GameKeepGuard == false)
            {
                return false;
            }

            #region Keep Guards
            //guard vs guard / npc
            if (attacker is GameKeepGuard)
            {
                if (defender is GameKeepGuard)
                    return false;

                if (defender is GameNPC && (defender as GameNPC)!.Brain is IControlledBrain == false)
                    return false;
            }

            //player vs guard
            if (defender is GameKeepGuard && attacker is GamePlayer
                && GameServer.KeepManager.IsEnemy(defender as GameKeepGuard, attacker as GamePlayer) == false)
            {
                if (quiet == false) MessageToLiving(attacker, LanguageMgr.GetTranslation((attacker as GamePlayer)?.Client, "ServerRules.PvPServerRules.AttackFriendlyNPC"));
                return false;
            }

            //guard vs player
            if (attacker is GameKeepGuard && defender is GamePlayer
                && GameServer.KeepManager.IsEnemy(attacker as GameKeepGuard, defender as GamePlayer) == false)
            {
                return false;
            }
            #endregion

            return true;
        }

        /// <summary>
        /// Is caster allowed to cast a spell
        /// </summary>
        /// <param name="caster"></param>
        /// <param name="target"></param>
        /// <param name="spell"></param>
        /// <param name="spellLine"></param>
        /// <returns>true if allowed</returns>
        public override bool IsAllowedToCastSpell(GameLiving caster, GameLiving target, Spell spell, SpellLine spellLine)
        {
            if (!base.IsAllowedToCastSpell(caster, target, spell, spellLine)) return false;

            GamePlayer casterPlayer = caster as GamePlayer;
            if (casterPlayer != null)
            {
                if (casterPlayer.IsInvulnerableToAttack)
                {
                    // always allow selftargeted spells
                    if (spell.Target == "self") return true;

                    // only caster can be the target, can't buff/heal other players
                    // PBAE/GTAE doesn't need a target so we check spell type as well
                    if (caster != target || spell.Target == "area" || spell.Target == "enemy" || (spell.Target == "group" && spell.SpellType != "SpeedEnhancement"))
                    {
                        MessageToLiving(caster, LanguageMgr.GetTranslation((caster as GamePlayer)?.Client, "ServerRules.PvPServerRules.OnlySelfCastWhileInvulnerable"), eChatType.CT_Important);
                        return false;
                    }
                }

            }
            return true;
        }

        public override bool IsSameRealm(GameLiving source, GameLiving target, bool quiet)
        {
            if (source == null || target == null)
                return false;
            
            // if controlled NPC - do checks for owner instead
            if (source is GameNPC)
            {
                var owner = source.GetLivingOwner();
                if (owner != null)
                {
                    source = owner;
                    quiet = true;
                }
            }
            if (target is GameNPC)
            {
                target = target.GetLivingOwner() ?? target;
            }

            if (source == target)
                return true;

            // mobs can heal mobs, players heal players/NPC
            if (source.Realm == 0 && target.Realm == 0) return true;

            GamePlayer? sourcePlayer = source as GamePlayer;
            GamePlayer? targetPlayer = target as GamePlayer;
            // clients with priv level > 1 are considered friendly by anyone
            if (targetPlayer != null && targetPlayer.Client.Account.PrivLevel > 1 && !Properties.ALLOW_GM_ATTACK) return true;
            // checking as a gm, targets are considered friendly
            if (sourcePlayer != null && sourcePlayer.Client.Account.PrivLevel > 1 && !Properties.ALLOW_GM_ATTACK) return true;

            //keep guards
            if (source is GameKeepGuard sourceGuard && targetPlayer != null)
            {
                if (!GameServer.KeepManager.IsEnemy(sourceGuard, targetPlayer))
                    return true;
            }

            if (target is GameKeepGuard targetGuard && sourcePlayer != null)
            {
                if (!GameServer.KeepManager.IsEnemy(targetGuard, sourcePlayer))
                    return true;
            }

            //doors need special handling
            if (target is GameKeepDoor targetDoor && sourcePlayer != null)
                return GameServer.KeepManager.IsEnemy(targetDoor, sourcePlayer);

            if (source is GameKeepDoor sourceDoor && targetPlayer != null)
                return GameServer.KeepManager.IsEnemy(sourceDoor, targetPlayer);

            //components need special handling
            if (target is GameKeepComponent targetComponent && sourcePlayer != null)
                return GameServer.KeepManager.IsEnemy(targetComponent, sourcePlayer);

            if (target is GameNPC targetNpc)
            {
                //CheckMobGroup
                if (sourcePlayer != null)
                {
                    if (MobGroups.MobGroup.IsQuestFriendly(targetNpc, sourcePlayer))
                    {
                        return true;
                    }
                }
                //Peace flag NPCs are same realm
                if (targetNpc.IsPeaceful)
                    return true;
            }

            if (source is GameNPC sourceNpc)
            {
                //CheckMobGroup
                if (targetPlayer != null)
                {
                    if (MobGroups.MobGroup.IsQuestFriendly(sourceNpc, targetPlayer))
                    {
                        return true;
                    }
                }
                if (sourceNpc.IsPeaceful)
                    return true;
            }


            if (sourcePlayer != null && targetPlayer != null)
                return true;

            if (sourcePlayer != null && target is GameNPC && target.Realm != 0)
                return true;

            if (quiet == false) MessageToLiving(source, LanguageMgr.GetTranslation((source as GamePlayer)?.Client, "ServerRules.PvPServerRules.TargetNotSameRealm", target.GetName(0, true)));
            return false;
        }

        public override bool IsAllowedCharsInAllRealms(GameClient client)
        {
            return true;
        }

        public override bool IsAllowedToGroup(GamePlayer source, GamePlayer target, bool quiet)
        {
            return true;
        }

        public override bool IsAllowedToJoinGuild(GamePlayer source, Guild guild)
        {
            return true;
        }

        public override bool IsAllowedToUnderstand(GameLiving source, GamePlayer target)
        {
            return true;
        }

        /// <summary>
        /// Gets the server type color handling scheme
        /// 
        /// ColorHandling: this byte tells the client how to handle color for PC and NPC names (over the head) 
        /// 0: standard way, other realm PC appear red, our realm NPC appear light green 
        /// 1: standard PvP way, all PC appear red, all NPC appear with their level color 
        /// 2: Same realm livings are friendly, other realm livings are enemy; nearest friend/enemy buttons work
        /// 3: standard PvE way, all PC friendly, realm 0 NPC enemy rest NPC appear light green 
        /// 4: All NPC are enemy, all players are friendly; nearest friend button selects self, nearest enemy don't work at all
        /// </summary>
        /// <param name="client">The client asking for color handling</param>
        /// <returns>The color handling</returns>
        public override byte GetColorHandling(GameClient client)
        {
            return 1;
        }

        /// <summary>
        /// Formats player statistics.
        /// </summary>
        /// <param name="player">The player to read statistics from.</param>
        /// <returns>List of strings.</returns>
        public override IList<string> FormatPlayerStatistics(GamePlayer player)
        {
            var stat = new List<string>();

            int total = 0;
            if (Properties.SHOW_NEW_PLAYER_STATS)
            {
                if (player.TaskXPlayer != null)
                {
                    string title = player.CurrentTitle != PlayerTitleMgr.ClearTitle ? player.CurrentTitle.GetDescription(player): "\r\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.TitleNone");
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.TitleAssigned") + ": " + "\r\n" + title);
                    string specialBonus = GetSpecialBonus(player.CurrentTitle, player);
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.TitleSpecialBonus") + ": " + "\r\n" + specialBonus);
                    stat.Add(" ");
                    if (Properties.PVP_ALLOW_TASKSPOINTS)
                    {
                        stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.StatsPVP"));
                        stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillEnemyPlayersGroup") + ": " + player.TaskXPlayer.KillEnemyPlayersGroupStats.ToString("F0"));
                        stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillEnemyPlayersAlone") + ": " + player.TaskXPlayer.KillEnemyPlayersAloneStats.ToString("F0"));
                        stat.Add(" ");
                        long totalPvP = player.TaskXPlayer.KillEnemyPlayersGroupStats + player.TaskXPlayer.KillEnemyPlayersAloneStats + player.TaskXPlayer.OutlawPlayersSentToJailStats;
                        stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Kill.TotalPlayers") + ": " + totalPvP.ToString("F0"));
                    }
                    else
                    {
                        stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.StatsCombatRvRPvE"));
                        stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillEnemyPlayersGroup") + ": " + player.TaskXPlayer.KillEnemyPlayersGroupStats.ToString("F0"));
                        stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillEnemyPlayersAlone") + ": " + player.TaskXPlayer.KillEnemyPlayersAloneStats.ToString("F0"));
                        stat.Add(" ");
                        stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.StatsCombatPvP"));
                        stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillEnemyPlayersGroup") + ": " + player.TaskXPlayer.KillEnemyPlayersPvPGroupStats.ToString("F0"));
                        stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillEnemyPlayersAlone") + ": " + player.TaskXPlayer.KillEnemyPlayersPvPAloneStats.ToString("F0"));
                        stat.Add(" ");
                        long totalKills = player.TaskXPlayer.KillEnemyPlayersGroupStats + player.TaskXPlayer.KillEnemyPlayersAloneStats + player.TaskXPlayer.OutlawPlayersSentToJailStats + player.TaskXPlayer.KillEnemyPlayersPvPGroupStats + player.TaskXPlayer.KillEnemyPlayersPvPAloneStats;
                        stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Kill.TotalPlayers") + ": " + totalKills.ToString("F0"));
                    }
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Deathblows.TotalPlayers") + ": " + total.ToString("F0"));
                    stat.Add(" ");
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.StatsRVR"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillKeepGuards") + ": " + player.TaskXPlayer.KillKeepGuardsStats.ToString("F0"));
                    if (player.CapturedKeeps > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.TakeKeeps") + ": " + player.CapturedKeeps.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.RvRChampionOfTheDay") + ": " + player.TaskXPlayer.RvRChampionOfTheDayStats.ToString("F0"));
                    stat.Add(" ");
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.StatsGVG"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillTerritoryGuards") + ": " + player.TaskXPlayer.KillTerritoryGuardsStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillTerritoryBoss") + ": " + player.TaskXPlayer.KillTerritoryBossStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.CapturedRelics") + ": " + player.CapturedRelics.ToString("F0"));
                    stat.Add(" ");
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.StatsPVE"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillCreaturesInDungeons") + ": " + player.TaskXPlayer.KillCreaturesInDungeonsStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillOutdoorsCreatures") + ": " + player.TaskXPlayer.KillOutdoorsCreaturesStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.TotalCreaturesKilled") + ": " + (player.TaskXPlayer.KillCreaturesInDungeonsStats + player.TaskXPlayer.KillOutdoorsCreaturesStats).ToString("F0"));
                    stat.Add(" ");
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.StatsCrafting", Properties.CRAFTING_TASKTOKEN_MINRECIPELVL));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.SuccessfulItemCombinations") + ": " + player.TaskXPlayer.SuccessfulItemCombinationsStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.MasteredCrafts") + ": " + player.TaskXPlayer.MasteredCraftsStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.MasterpieceCrafted") + ": " + player.TaskXPlayer.MasterpieceCraftedStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.TotalCraftPerformed") + ": " + (player.TaskXPlayer.SuccessfulItemCombinationsStats + player.TaskXPlayer.MasteredCraftsStats).ToString("F0"));
                    stat.Add(" ");
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.StatsGreatAchievements"));
                    if (player.KillsDragon > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.PvE.KillsDragon") + ": " + player.KillsDragon.ToString("F0"));
                    if (player.KillsEpicBoss > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.PvE.KillsEpic") + ": " + player.KillsEpicBoss.ToString("F0"));
                    stat.Add(" ");
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.StatsSpecialAchievements"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.ItemsSoldToPlayers") + ": " + player.TaskXPlayer.ItemsSoldToPlayersStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.SuccessfulPvPThefts") + ": " + player.TaskXPlayer.SuccessfulPvPTheftsStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.OutlawPlayersSentToJail") + ": " + player.TaskXPlayer.OutlawPlayersSentToJailStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.EnemiesKilledInAdrenalineMode") + ": " + player.TaskXPlayer.EnemiesKilledInAdrenalineModeStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.EnemyKilledInDuel") + ": " + player.TaskXPlayer.EnemyKilledInDuelStats.ToString("F0"));
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.QuestsCompleted") + ": " + player.TaskXPlayer.QuestsCompletedStats.ToString("F0"));
                    stat.Add(" ");
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.StatsMiscellaneous"));
                    //stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.PlayersAssassinated") + ": " + player.TaskXPlayer.AssassinationKillsStats.ToString("F0"));
                    long totalLifetimeDeaths = player.DBCharacter.DeathCount + player.DBCharacter.DeathsPvP;
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.TotalDeathCount") + ": " + totalLifetimeDeaths.ToString("F0"));
                }
            }
            else
            {
                #region Players Killed
                //only show if there is a kill [by Suncheck]
                if ((player.KillsAlbionPlayers + player.KillsMidgardPlayers + player.KillsHiberniaPlayers) > 0)
                {
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Kill.Title"));
                    switch ((eRealm)player.Realm)
                    {
                        case eRealm.Albion:
                            if (player.KillsMidgardPlayers > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Kill.MidgardPlayer") + ": " + player.KillsMidgardPlayers.ToString("F0"));
                            if (player.KillsHiberniaPlayers > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Kill.HiberniaPlayer") + ": " + player.KillsHiberniaPlayers.ToString("F0"));
                            total = player.KillsMidgardPlayers + player.KillsHiberniaPlayers;
                            break;
                        case eRealm.Midgard:
                            if (player.KillsAlbionPlayers > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Kill.AlbionPlayer") + ": " + player.KillsAlbionPlayers.ToString("F0"));
                            if (player.KillsHiberniaPlayers > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Kill.HiberniaPlayer") + ": " + player.KillsHiberniaPlayers.ToString("F0"));
                            total = player.KillsAlbionPlayers + player.KillsHiberniaPlayers;
                            break;
                        case eRealm.Hibernia:
                            if (player.KillsAlbionPlayers > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Kill.AlbionPlayer") + ": " + player.KillsAlbionPlayers.ToString("F0"));
                            if (player.KillsMidgardPlayers > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Kill.MidgardPlayer") + ": " + player.KillsMidgardPlayers.ToString("F0"));
                            total = player.KillsMidgardPlayers + player.KillsAlbionPlayers;
                            break;
                    }
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Kill.TotalPlayers") + ": " + total.ToString("F0"));
                }
                #endregion
                stat.Add(" ");
                #region Players Deathblows
                //only show if there is a kill [by Suncheck]
                if ((player.KillsAlbionDeathBlows + player.KillsMidgardDeathBlows + player.KillsHiberniaDeathBlows) > 0)
                {
                    total = 0;
                    switch ((eRealm)player.Realm)
                    {
                        case eRealm.Albion:
                            if (player.KillsMidgardDeathBlows > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Deathblows.MidgardPlayer") + ": " + player.KillsMidgardDeathBlows.ToString("F0"));
                            if (player.KillsHiberniaDeathBlows > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Deathblows.HiberniaPlayer") + ": " + player.KillsHiberniaDeathBlows.ToString("F0"));
                            total = player.KillsMidgardDeathBlows + player.KillsHiberniaDeathBlows;
                            break;
                        case eRealm.Midgard:
                            if (player.KillsAlbionDeathBlows > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Deathblows.AlbionPlayer") + ": " + player.KillsAlbionDeathBlows.ToString("F0"));
                            if (player.KillsHiberniaDeathBlows > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Deathblows.HiberniaPlayer") + ": " + player.KillsHiberniaDeathBlows.ToString("F0"));
                            total = player.KillsAlbionDeathBlows + player.KillsHiberniaDeathBlows;
                            break;
                        case eRealm.Hibernia:
                            if (player.KillsAlbionDeathBlows > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Deathblows.AlbionPlayer") + ": " + player.KillsAlbionDeathBlows.ToString("F0"));
                            if (player.KillsMidgardDeathBlows > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Deathblows.MidgardPlayer") + ": " + player.KillsMidgardDeathBlows.ToString("F0"));
                            total = player.KillsMidgardDeathBlows + player.KillsAlbionDeathBlows;
                            break;
                    }
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Deathblows.TotalPlayers") + ": " + total.ToString("F0"));
                }
                #endregion
                stat.Add(" ");
                #region Players Solo Kills
                //only show if there is a kill [by Suncheck]
                if ((player.KillsAlbionSolo + player.KillsMidgardSolo + player.KillsHiberniaSolo) > 0)
                {
                    total = 0;
                    switch ((eRealm)player.Realm)
                    {
                        case eRealm.Albion:
                            if (player.KillsMidgardSolo > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Solo.MidgardPlayer") + ": " + player.KillsMidgardSolo.ToString("F0"));
                            if (player.KillsHiberniaSolo > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Solo.HiberniaPlayer") + ": " + player.KillsHiberniaSolo.ToString("F0"));
                            total = player.KillsMidgardSolo + player.KillsHiberniaSolo;
                            break;
                        case eRealm.Midgard:
                            if (player.KillsAlbionSolo > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Solo.AlbionPlayer") + ": " + player.KillsAlbionSolo.ToString("F0"));
                            if (player.KillsHiberniaSolo > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Solo.HiberniaPlayer") + ": " + player.KillsHiberniaSolo.ToString("F0"));
                            total = player.KillsAlbionSolo + player.KillsHiberniaSolo;
                            break;
                        case eRealm.Hibernia:
                            if (player.KillsAlbionSolo > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Solo.AlbionPlayer") + ": " + player.KillsAlbionSolo.ToString("F0"));
                            if (player.KillsMidgardSolo > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Solo.MidgardPlayer") + ": " + player.KillsMidgardSolo.ToString("F0"));
                            total = player.KillsMidgardSolo + player.KillsAlbionSolo;
                            break;
                    }
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Solo.TotalPlayers") + ": " + total.ToString("F0"));
                }
                #endregion
                stat.Add(" ");
                #region Keeps
                //only show if there is a capture [by Suncheck]
                if ((player.CapturedKeeps + player.CapturedTowers) > 0)
                {
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Capture.Title"));
                    //stat.Add("Relics Taken: " + player.RelicsTaken.ToString("F0"));
                    //stat.Add("Albion Keeps Captured: " + player.CapturedAlbionKeeps.ToString("F0"));
                    //stat.Add("Midgard Keeps Captured: " + player.CapturedMidgardKeeps.ToString("F0"));
                    //stat.Add("Hibernia Keeps Captured: " + player.CapturedHiberniaKeeps.ToString("F0"));
                    if (player.CapturedKeeps > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Capture.Keeps") + ": " + player.CapturedKeeps.ToString("F0"));
                    //stat.Add("Keep Lords Slain: " + player.KeepLordsSlain.ToString("F0"));
                    //stat.Add("Albion Towers Captured: " + player.CapturedAlbionTowers.ToString("F0"));
                    //stat.Add("Midgard Towers Captured: " + player.CapturedMidgardTowers.ToString("F0"));
                    //stat.Add("Hibernia Towers Captured: " + player.CapturedHiberniaTowers.ToString("F0"));
                    if (player.CapturedTowers > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.Capture.Towers") + ": " + player.CapturedTowers.ToString("F0"));
                    //stat.Add("Tower Captains Slain: " + player.TowerCaptainsSlain.ToString("F0"));
                    //stat.Add("Realm Guard Kills Albion: " + player.RealmGuardTotalKills.ToString("F0"));
                    //stat.Add("Realm Guard Kills Midgard: " + player.RealmGuardTotalKills.ToString("F0"));
                    //stat.Add("Realm Guard Kills Hibernia: " + player.RealmGuardTotalKills.ToString("F0"));
                    //stat.Add("Total Realm Guard Kills: " + player.RealmGuardTotalKills.ToString("F0"));
                }
                #endregion
                stat.Add(" ");
                #region PvE
                //only show if there is a kill [by Suncheck]
                if ((player.KillsDragon + player.KillsEpicBoss + player.KillsLegion) > 0)
                {
                    stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.PvE.Title"));
                    if (player.KillsDragon > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.PvE.KillsDragon") + ": " + player.KillsDragon.ToString("F0"));
                    if (player.KillsEpicBoss > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.PvE.KillsEpic") + ": " + player.KillsEpicBoss.ToString("F0"));
                    if (player.KillsLegion > 0) stat.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerStatistic.PvE.KillsLegion") + ": " + player.KillsLegion.ToString("F0"));
                }
                #endregion
            }

            return stat;
        }

        /// <summary>
        /// Reset the keep with special server rules handling
        /// </summary>
        /// <param name="lord">The lord that was killed</param>
        /// <param name="killer">The lord's killer</param>
        public override void ResetKeep(GuardLord lord, GameObject killer)
        {
            base.ResetKeep(lord, killer);
            eRealm realm = eRealm.None;

            //pvp servers, the realm changes to the group leaders realm
            if (killer is GamePlayer)
            {
                Group group = ((killer as GamePlayer)!.Group);
                if (group != null)
                    realm = (eRealm)group.Leader.Realm;
                else realm = (eRealm)killer.Realm;
            }
            else if (killer is GameNPC && killer.GetLivingOwner() is {} owner)
            {
                Group group = null;
                if (owner is GamePlayer { Group: not null })
                    group = owner.Group;
                if (group != null)
                    realm = (eRealm)group.Leader.Realm;
                else realm = (eRealm)killer.Realm;
            }
            lord.Component.Keep.Reset(realm);
        }
    }
}
