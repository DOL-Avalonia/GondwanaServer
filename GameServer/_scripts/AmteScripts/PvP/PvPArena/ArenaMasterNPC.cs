using AmteScripts.Managers;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;

namespace DOL.GS.Scripts
{
    public class ArenaMasterNPC : GameNPC
    {
        public override bool AddToWorld()
        {
            this.Name = "Arena Master";
            this.GuildName = "Tournament Setup";
            return base.AddToWorld();
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            TurnTo(player);

            var session = ArenaManager.Instance.GetOrCreateSession(this.CurrentRegionID, this);
            string lang = player.Client.Account.Language;

            if (!ArenaManager.Instance.HasSpawns(this.CurrentRegionID))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.NotActive"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (session.State == ArenaManager.eArenaState.Cooldown)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.Cooldown"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }


            if (session.State == ArenaManager.eArenaState.Running)
            {
                string msg = LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.InProgress") + "\n";
                if (session.CurrentTeamA != null && session.CurrentTeamB != null && session.IsBettingOpen)
                {
                    msg += "\n--- " + LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.CurrentMatch") + " ---\n";
                    msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.CurrentMatch1", session.CurrentTeamA.TeamName) + "\n";
                    msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.CurrentMatch2", session.CurrentTeamB.TeamName) + "\n\n";

                    msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.BettingOpen") + "\n";
                    msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.BettingCmd") + "\n\n";
                    msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.BettingWarning");
                }
                else
                {
                    msg += "\n" + LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.MatchUnderway");
                }

                if (player.Client.Account.PrivLevel > 1)
                {
                    msg += "\n\n=== " + LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.GMMenuTitle") + " ===\n";
                    msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.GMMenuShutdown");

                    if (player.TempProperties.getProperty<bool>("ArenaParticipant", false))
                    {
                        msg += "\n" + LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.GMSwitchSpec") + "\n";
                        msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.GMSwitchFight");
                    }
                    else
                    {
                        msg += "\n" + LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.GMJoinSpec") + "\n";
                        msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.GMJoinFight");
                    }
                }
                player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (session.State == ArenaManager.eArenaState.Idle)
            {
                string msg = LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.IdleGreetings") + "\n\n";
                msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.OptSolo") + "\n";
                msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.Opt2v2") + "\n";
                msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.Opt3v3") + "\n";
                msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.Opt4v4");

                if (player.Client.Account.PrivLevel > 1)
                {
                    msg += "\n\n=== " + LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.GMMenuTitle") + " ===\n";
                    msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.GMStartSolo") + "\n";
                    msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.GMStartSpec");
                }

                player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (session.State == ArenaManager.eArenaState.Queuing)
            {
                if (player.TempProperties.getProperty<bool>("ArenaQueued", false))
                {
                    string msg = LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.InQueue") + "\n";
                    msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.LeaveQueue");
                    player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
                else
                {
                    string modeStr = session.Mode == ArenaManager.eArenaMode.Solo ? LanguageMgr.GetTranslation(lang, "ArenaManager.SoloVsSolo") : $"{(int)session.TeamSize} vs {(int)session.TeamSize}";
                    string msg = LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.Forming", modeStr) + "\n";
                    msg += LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.JoinQueue");
                    player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
            }

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text)) return false;
            if (!(source is GamePlayer player)) return false;

            var session = ArenaManager.Instance.GetOrCreateSession(this.CurrentRegionID, this);
            string choice = text.ToLower();
            string lang = player.Client.Account.Language;

            if (player.Client.Account.PrivLevel > 1 &&
               (choice == "force shutdown" ||
                choice == "debug: start solo match" ||
                choice == "debug: start as spectator" ||
                choice == "debug: switch as spectator" ||
                choice == "debug: switch as fighter" ||
                choice == "debug: join as a spectator" ||
                choice == "debug: join as a fighter"))
            {
                ArenaManager.Instance.HandleDebugCommand(session, player, choice);
                return true;
            }

            if (session.State == ArenaManager.eArenaState.Idle)
            {
                ArenaManager.eArenaMode mode = ArenaManager.eArenaMode.None;

                string soloMatch = LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.OptSolo").Replace("[", "").Replace("]", "").ToLower();
                string twoVsTwo = LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.Opt2v2").Replace("[", "").Replace("]", "").ToLower();
                string threeVsThree = LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.Opt3v3").Replace("[", "").Replace("]", "").ToLower();
                string fourVsFour = LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.Opt4v4").Replace("[", "").Replace("]", "").ToLower();

                if (choice == soloMatch || choice == "solo vs solo" || choice == "en solo") mode = ArenaManager.eArenaMode.Solo;
                else if (choice == twoVsTwo || choice == "2 vs 2" || choice == "2 contre 2") mode = ArenaManager.eArenaMode.TwoVsTwo;
                else if (choice == threeVsThree || choice == "3 vs 3" || choice == "3 contre 3") mode = ArenaManager.eArenaMode.ThreeVsThree;
                else if (choice == fourVsFour || choice == "4 vs 4" || choice == "4 contre 4") mode = ArenaManager.eArenaMode.FourVsFour;

                if (mode != ArenaManager.eArenaMode.None)
                {
                    ArenaManager.Instance.StartQueue(session, mode);
                    ArenaManager.Instance.EnqueueSolo(session, player);
                }
            }
            else if (session.State == ArenaManager.eArenaState.Queuing)
            {
                string joinCmd = LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.JoinQueue").Replace("[", "").Replace("]", "").ToLower();
                string leaveCmd = LanguageMgr.GetTranslation(lang, "ArenaMaster.Interact.LeaveQueue").Replace("[", "").Replace("]", "").ToLower();

                if (choice == joinCmd || choice == "join queue" || choice == "rejoindre la file d'attente")
                {
                    if (player.TempProperties.getProperty<bool>("ArenaQueued", false)) return true;

                    if (session.Mode == ArenaManager.eArenaMode.Solo)
                    {
                        if (player.Group != null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ArenaMaster.Whisper.GroupSolo"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            return true;
                        }
                        ArenaManager.Instance.EnqueueSolo(session, player);
                    }
                    else // Group/Multiplayer Queuing
                    {
                        if (player.Group != null)
                        {
                            if (player.Group.Leader != player)
                            {
                                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ArenaMaster.Whisper.NotLeader"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                                return true;
                            }
                            if (player.Group.MemberCount != session.TeamSize)
                            {
                                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ArenaMaster.Whisper.GroupSize", session.TeamSize), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                                return true;
                            }
                            ArenaManager.Instance.EnqueueGroup(session, player.Group);
                        }
                        else
                        {
                            ArenaManager.Instance.EnqueueSolo(session, player);
                            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ArenaMaster.Whisper.AutoBalance"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }
                }
                else if (choice == leaveCmd || choice == "leave queue" || choice == "quitter la file d'attente")
                {
                    ArenaManager.Instance.DequeuePlayer(session, player, true);
                }
            }
            return true;
        }
    }
}