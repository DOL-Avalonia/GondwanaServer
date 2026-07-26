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

using DOL.AI.Brain;
using DOL.GS.Quests;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Keeps
{
    /// <summary>
    /// Represents a mission master
    /// </summary>
    public class MissionMaster : GameKeepGuard
    {
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            if (Component == null)
                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Interact.Greeting1", player.Name, player.Salutation));
            else
                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Interact.Greeting2", player.Name));

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str))
                return false;

            GamePlayer player = source as GamePlayer;
            if (player == null)
                return false;

            if (!GameServer.ServerRules.IsSameRealm(this, player, true))
            {
                return false;
            }

            string lowerText = str.ToLower();

            if (lowerText.StartsWith("tower capture") || lowerText.StartsWith("capture de tour"))
            {
                if (player.Group == null)
                {
                    _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotInGroup"));
                }
                else if (player.Group.Leader != player)
                {
                    _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotGroupLeader"));
                }
                else
                {
                    if (player.Group.Mission != null)
                        player.Group.Mission.ExpireMission();

                    string target = lowerText.Replace("tower capture", "").Replace("capture de tour", "").Trim();
                    player.Group.Mission = new CaptureMission(CaptureMission.eCaptureType.Tower, player.Group, target);
                }
            }
            else if (lowerText.StartsWith("keep capture") || lowerText.StartsWith("capture de fort"))
            {
                if (player.Group == null)
                {
                    _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotInGroup"));
                }
                else if (player.Group.Leader != player)
                {
                    _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotGroupLeader"));
                }
                else
                {
                    if (player.Group.Mission != null)
                        player.Group.Mission.ExpireMission();

                    string target = lowerText.Replace("keep capture", "").Replace("capture de fort", "").Trim();
                    player.Group.Mission = new CaptureMission(CaptureMission.eCaptureType.Keep, player.Group, target);
                }
            }
            else
            {
                switch (lowerText)
                {
                    case "realm":
                    case "royaume":
                        {
                            if (Component == null)
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.Realm.ComponentNull"));
                            else
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.Realm.ComponentNotNull"));
                            break;
                        }
                    case "personal missions":
                    case "missions personnelles":
                        {
                            _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.PersonalMissions"));
                            break;
                        }
                    case "realm guards":
                    case "gardes du royaume":
                        {
                            if (player.Mission != null)
                                player.Mission.ExpireMission();
                            player.Mission = new KillMission(typeof(GameKeepGuard), 15, "enemy realm guards", player);
                            break;
                        }
                    case "enemies of the realm":
                    case "ennemis du royaume":
                        {
                            if (player.Mission != null)
                                player.Mission.ExpireMission();
                            player.Mission = new KillMission(typeof(GamePlayer), 5, "enemy players", player);
                            break;
                        }
                    case "reconnoiter":
                    case "reconnaître":
                        {
                            if (player.Mission != null)
                                player.Mission.ExpireMission();
                            player.Mission = new ScoutMission(player);
                            break;
                        }
                    case "assassination":
                    case "assassinat":
                        {
                            _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotImplemented"));
                            break;
                        }
                    case "group missions":
                    case "missions de groupe":
                        {
                            if (player.Group == null)
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotInGroup"));
                                break;
                            }

                            if (player.Group.Leader != player)
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotGroupLeader"));
                                break;
                            }

                            _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.GroupMissions"));
                            break;
                        }
                    case "tower raize":
                    case "raser la tour":
                        {
                            if (player.Group == null)
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotInGroup"));
                                break;
                            }

                            if (player.Group.Leader != player)
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotGroupLeader"));
                                break;
                            }
                            player.Group.Mission = new RaizeMission(player.Group);
                            break;
                        }
                    case "tower capture":
                    case "capture de tour":
                        {
                            break;
                        }
                    case "keep capture":
                    case "capture de fort":
                        {
                            break;
                        }
                    case "caravan":
                    case "caravane":
                        {
                            if (player.Group == null)
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotInGroup"));
                                break;
                            }

                            if (player.Group.Leader != player)
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotGroupLeader"));
                                break;
                            }
                            _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotImplemented"));
                            break;
                        }
                    case "enemy guards":
                    case "gardes ennemis":
                        {
                            if (player.Group == null)
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotInGroup"));
                                break;
                            }

                            if (player.Group.Leader != player)
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotGroupLeader"));
                                break;
                            }
                            if (player.Group.Mission != null)
                                player.Group.Mission.ExpireMission();
                            player.Group.Mission = new KillMission(typeof(GameKeepGuard), 25, "enemy realm guards", player.Group);
                            break;
                        }
                    case "realm enemies":
                    case "ennemis de royaume":
                        {
                            if (player.Group == null)
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotInGroup"));
                                break;
                            }

                            if (player.Group.Leader != player)
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotGroupLeader"));
                                break;
                            }
                            if (player.Group.Mission != null)
                                player.Group.Mission.ExpireMission();
                            player.Group.Mission = new KillMission(typeof(GamePlayer), 15, "enemy players", player.Group);
                            break;
                        }
                    case "guild missions":
                    case "missions de guilde":
                        {
                            if (Component != null)
                                break;
                            if (player.Guild == null)
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.Guild.NoGuild"));
                                return false;
                            }

                            if (!player.Guild.HasRank(player, Guild.eRank.OcSpeak))
                            {
                                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.Guild.RankTooLow"));
                                return false;
                            }
                            //TODO: implement guild missions
                            _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.NotImplemented"));
                            _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "MissionMaster.Whisper.Guild.Missions"));
                            break;
                        }
                }
            }

            if (player.Mission != null)
                _ = SayTo(player, player.Mission.Description);

            if (player.Group != null && player.Group.Mission != null)
                _ = SayTo(player, player.Group.Mission.Description);

            return true;
        }

        protected override CharacterClass GetClass()
        {
            if (ModelRealm == eRealm.Albion) return CharacterClass.Armsman;
            else if (ModelRealm == eRealm.Midgard) return CharacterClass.Warrior;
            else if (ModelRealm == eRealm.Hibernia) return CharacterClass.Hero;
            return CharacterClass.None;
        }

        protected override void SetBlockEvadeParryChance()
        {
            base.SetBlockEvadeParryChance();

            BlockChance = 15;
            ParryChance = 15;

            if (ModelRealm != eRealm.Albion)
            {
                EvadeChance = 10;
                ParryChance = 5;
            }
        }

        protected override void SetRespawnTime()
        {
            RespawnInterval = 120000;
        }

        protected override void SetAggression()
        {
            (Brain as KeepGuardBrain)!.SetAggression(90, 400);
        }

        protected override void SetName()
        {
            switch (ModelRealm)
            {
                case eRealm.None:
                case eRealm.Albion:
                    Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.CaptainCommander");
                    break;
                case eRealm.Midgard:
                    Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.HersirCommander");
                    break;
                case eRealm.Hibernia:
                    Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.ChampionCommander");
                    break;
            }

            if (Realm == eRealm.None)
            {
                Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.Renegade", Name);
            }
        }
    }

    /*
	 * Champion Commander
	 * Captain Commander
	 * Hersir Commander
	 * 
	 * Hail and well met, PLAYERNAME! As the leader of our forces, I am calling upon our finest warriors to aid in the vanquishing of our enemies. Do you wish to do your duty in defence of our [realm]?
	 * Excellent! We all must do our part. How would you like to assist the cause? I have [personal missions] and [group missions] available.
	 * 
	 * General
	 * 
	 * Greetings, PLAYERNAME. We have put out the call far and wide for heroes such as yourself to aid us in our ongoing struggle. It warms my heart good to to see a great CLASSNAME such as yourself willing to lay their life on the line in defence of the [realm].
	 * We all must do our part. How would you like to assist the cause? I have [personal missions], [group missions], and [guild missions] available.
	 * We have several personal missions from which to choose. Would you like to claim the bounty on some [realm guards], or claim the bounties on some [enemies of the realm]? Perhaps a frontal assault isn't your style? If so, we also have missions that require you to [reconnoiter] an enemy realm, or elimate the thread of an impending [assassination]?
	 * Would your group like to help with a [tower capture], a [keep capture], or a [caravan] raid? Should those choices fail to appeal to you, I also have bounty missions on [enemy guards] and [realm enemies] if that is your preference.
	 * Outstanding, we can always use help from organized guilds. Would you like to press the attack on the realm of [Albion] or the realm of [Hibernia].
	 */
}
