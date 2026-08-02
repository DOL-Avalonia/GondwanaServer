using System;
using DOL.GS;
using DOL.GS.PacketHandler;
using AmteScripts.Managers;

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

            if (!ArenaManager.Instance.HasSpawns(this.CurrentRegionID))
            {
                player.Out.SendMessage("The arena is not active or under repair (missing spawn points) in this region.", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (session.State == ArenaManager.eArenaState.Cooldown)
            {
                player.Out.SendMessage("The arena is currently being cleaned. The next session will be available shortly.", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (session.State == ArenaManager.eArenaState.Running)
            {
                string msg = "An Arena Contest is currently in progress.\n";
                if (session.CurrentTeamA != null && session.CurrentTeamB != null && session.IsBettingOpen)
                {
                    msg += $"\n--- Current Match ---\n[1] {session.CurrentTeamA.TeamName}\n[2] {session.CurrentTeamB.TeamName}\n";
                    msg += "\nBetting is OPEN! Use the command:\n/bet <1 or 2> <gold>\n";
                    msg += "\n(Warning: You can bet more gold than you have! Doing so draws from the Bank on credit. If you lose, your bank goes negative, risking asset seizure and jail!)";
                }
                else
                {
                    msg += "\nA match is underway! Wait for the next round to place your bets. Use /bet list to see the current pool.";
                }

                if (player.Client.Account.PrivLevel > 1)
                {
                    msg += "\n\n=== GM Debug Menu ===\n[Force Shutdown]";

                    if (player.TempProperties.getProperty<bool>("ArenaParticipant", false))
                    {
                        msg += "\n[Debug: Switch as Spectator]\n[Debug: Switch as Fighter]";
                    }
                    else
                    {
                        msg += "\n[Debug: Join as a Spectator]\n[Debug: Join as a Fighter]";
                    }
                }
                player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (session.State == ArenaManager.eArenaState.Idle)
            {
                string msg = "Hail warrior! The arena is idle. Choose the tournament bracket size to begin queuing:\n\n" +
                             "[Solo vs Solo]\n[2 vs 2]\n[3 vs 3]\n[4 vs 4]";

                if (player.Client.Account.PrivLevel > 1)
                {
                    msg += "\n\n=== GM Debug Menu ===\n[Debug: Start Solo Match]\n[Debug: Start as Spectator]";
                }

                player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (session.State == ArenaManager.eArenaState.Queuing)
            {
                if (player.TempProperties.getProperty<bool>("ArenaQueued", false))
                {
                    player.Out.SendMessage("You are currently in the queue.\n[Leave Queue]", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
                else
                {
                    string modeStr = session.Mode == ArenaManager.eArenaMode.Solo ? "Solo vs Solo" : $"{(int)session.TeamSize} vs {(int)session.TeamSize}";
                    string msg = $"An Arena session ({modeStr}) is currently forming!\n[Join Queue]";
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
                if (choice == "solo vs solo") mode = ArenaManager.eArenaMode.Solo;
                else if (choice == "2 vs 2") mode = ArenaManager.eArenaMode.TwoVsTwo;
                else if (choice == "3 vs 3") mode = ArenaManager.eArenaMode.ThreeVsThree;
                else if (choice == "4 vs 4") mode = ArenaManager.eArenaMode.FourVsFour;
                
                if (mode != ArenaManager.eArenaMode.None)
                {
                    ArenaManager.Instance.StartQueue(session, mode);
                    ArenaManager.Instance.EnqueueSolo(session, player);
                }
            }
            else if (session.State == ArenaManager.eArenaState.Queuing)
            {
                if (choice == "join queue")
                {
                    if (player.TempProperties.getProperty<bool>("ArenaQueued", false)) return true;

                    if (session.Mode == ArenaManager.eArenaMode.Solo)
                    {
                        if (player.Group != null)
                        {
                            player.Out.SendMessage("This is a solo arena. Disband your group first.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
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
                                player.Out.SendMessage("Only the group leader can subscribe the group.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                                return true;
                            }
                            if (player.Group.MemberCount != session.TeamSize)
                            {
                                player.Out.SendMessage($"This arena requires exactly {session.TeamSize} members in your group.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                                return true;
                            }
                            ArenaManager.Instance.EnqueueGroup(session, player.Group);
                        }
                        else
                        {
                            ArenaManager.Instance.EnqueueSolo(session, player);
                            player.Out.SendMessage("You joined the Arena Queue individually and will be auto-balanced into a team later.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }
                }
                else if (choice == "leave queue")
                {
                    ArenaManager.Instance.DequeuePlayer(session, player, true);
                }
            }
            return true;
        }
    }
}