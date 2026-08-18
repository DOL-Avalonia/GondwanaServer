using System;
using System.Linq;
using DOL.Language;
using DOL.GS.Commands;
using AmteScripts.Managers;
using DOL.GS;
using DOL.GS.PacketHandler;
using AmteScripts.PvP.Rewards;
using DOL.GS.Scripts;

namespace DOL.GS.Commands
{
    /// <summary>
    /// The new /pvp command for GMs or players to manage or view PvP state
    /// </summary>
    [Cmd(
        "&pvp",
        ePrivLevel.Player, // or ePrivLevel.Player if you want everyone to see subcommands like "info"
        "Commands.GM.PvP.Description",
        "Commands.GM.PvP.Usage.Info",
        "Commands.GM.PvP.Usage.Scores")]
    [Cmd(
        "&pvp",
        ePrivLevel.GM,
        "Commands.GM.PvP.Description",
        "Commands.GM.PvP.Usage.Open",
        "Commands.GM.PvP.Usage.Close",
        "Commands.GM.PvP.Usage.Unforce",
        "Commands.GM.PvP.Usage.Status",
        "Commands.GM.PvP.Usage.Refresh",
        "Commands.GM.PvP.Usage.Reset",
        "Commands.GM.PvP.Usage.SpawnDebugChests")]
    public class PvpCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length <= 1)
            {
                DisplaySyntax(client);
                return;
            }

            string subcmd = args[1].ToLower();

            string sessionID = string.Empty;
            switch (subcmd)
            {
                case "open" when client.Account.PrivLevel >= 2:
                    {
                        if (args.Length >= 3)
                        {
                            sessionID = args[2];
                        }
                        
                        if (PvpManager.Instance.IsOpen)
                        {
                            var msg = LanguageMgr.GetTranslation(
                                client.Account.Language,
                                "Commands.GM.PvP.AlreadyOpen",
                                PvpManager.Instance.CurrentSessionId,
                                string.Join(',', PvpManager.Instance.CurrentZones.Select(z => z.Description + "(" + z.ID + ")"))
                            );
                            DisplayMessage(client, msg);
                            return;
                        }

                        bool success = false;
                        try
                        {
                            success = PvpManager.Instance.Open(sessionID, true);
                        }
                        catch
                        {
                            client.SendTranslation("Commands.GM.PvP.PvPNotOpened");
                            throw;
                        }
                        if (success)
                        {
                            var msg = LanguageMgr.GetTranslation(
                                client.Account.Language,
                                "Commands.GM.PvP.PvPOpened",
                                PvpManager.Instance.CurrentSessionId,
                                string.Join(',', PvpManager.Instance.CurrentZones.Select(z => z.Description + "(" + z.ID + ")")),
                                string.Join(',', PvpManager.Instance.CurrentZones.Select(z => z.ZoneRegion).Distinct().Select(r => r.Description + "(" + r.ID + ")"))
                            );
                            DisplayMessage(client, msg);
                        }
                        else
                        {
                            DisplayMessage(
                                client,
                                string.IsNullOrEmpty(sessionID) ? LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.PvP.PvPNotOpened") : LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.PvP.PvPNotFound", sessionID)
                            );
                        }
                        break;
                    }

                case "close" when client.Account.PrivLevel >= 2:
                    {
                        bool success = PvpManager.Instance.Close();
                        if (success)
                        {
                            DisplayMessage(client,
                                LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.PvPClosed"));
                        }
                        else
                        {
                            DisplayMessage(client,
                                LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.PvPNotClosed"));
                        }
                        break;
                    }

                case "unforce" when client.Account.PrivLevel >= 2:
                    {
                        if (!PvpManager.Instance.IsOpen)
                        {
                            DisplayMessage(client,
                                LanguageMgr.GetTranslation(client.Account.Language,
                                    "RvRManager.PvPUnforceNotPossible"));
                            break;
                        }
                        // We do an "unforce" by calling Open(...) again with force=false
                        PvpManager.Instance.Open(string.Empty, false);
                        DisplayMessage(client,
                            LanguageMgr.GetTranslation(client.Account.Language,
                                "RvRManager.PvPWillCloseAutomatically"));
                        break;
                    }

                case "status" when client.Account.PrivLevel >= 2:
                    {
                        // Show the PvP status
                        // Example text
                        string status = PvpManager.Instance.IsOpen
                            ? "open, zones: " + string.Join(',', PvpManager.Instance.CurrentZones.Select(z => z.Description + "(" + z.ID + ")")) +
                              " regions: " + string.Join(',', PvpManager.Instance.CurrentZones.Select(z => z.ZoneRegion).Distinct().Select(r => r.Description + "(" + r.ID + ")"))
                            : "closed";
                        DisplayMessage(client,
                            LanguageMgr.GetTranslation(client.Account.Language,
                                "RvRManager.PvPStatus", status));
                        break;
                    }

                case "refresh" when client.Account.PrivLevel >= 2:
                    {
                        if (PvpManager.Instance.IsOpen)
                        {
                            DisplayMessage(client,
                                LanguageMgr.GetTranslation(client.Account.Language,
                                    "RvRManager.PvPMustBeClosed"));
                            break;
                        }
                        // re-scan or reload DB sessions
                        PvpSessionMgr.ReloadSessions();
                        var pvpMaps = string.Join(
                            ", ",
                            PvpSessionMgr.GetAllSessions().Select(s => s.SessionID + " => [" + s.ZoneList + "]")
                        );
                        DisplayMessage(client,
                            LanguageMgr.GetTranslation(client.Account.Language,
                                "RvRManager.PvPMapsUsed", pvpMaps));
                        break;
                    }

                case "reset" when client.Account.PrivLevel >= 2:
                    var previous = PvpManager.Instance.CurrentSession?.SessionID;
                    if (string.IsNullOrEmpty(previous))
                    {
                        DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.PvPCannotReset", previous));
                        return;
                    }
                    PvpManager.Instance.Close();
                    PvpManager.Instance.Open(previous, false);
                    DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.PvPReset"));
                    break;

                default:
                    DisplaySyntax(client);
                    break;

                case "spawndebugchests" when client.Account.PrivLevel >= 2:
                    {
                        var targetNPC = client.Player.TargetObject as GameNPC;
                        
                        eRewardTier selectedTier;
                        string expectedPrefix;

                        if (targetNPC is TeleporterPvP)
                        {
                            selectedTier = eRewardTier.PvPTier1; // Yields 'Legendary' with high scores
                            expectedPrefix = "Legendary";
                        }
                        else if (targetNPC is TeleporterRvR)
                        {
                            selectedTier = eRewardTier.RvRFinest; // Yields 'Mythical' with high scores
                            expectedPrefix = "Mythical";
                        }
                        else
                        {
                            DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.PvP.Spawns.SelectTeleporter"));
                            return;
                        }

                        int fakePeakPlayers = 100;
                        int fakeScore = 100;
                        int fakeNeededPlayers = 10;

                        bool spawned = RewardChestSpawner.SpawnChestsForPlayer(
                            client.Player,
                            selectedTier, 
                            targetNPC,
                            fakePeakPlayers,
                            fakeScore,
                            fakeNeededPlayers
                        );

                        if (spawned)
                        {
                            DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.PvP.Spawns.Success", expectedPrefix));
                        }
                        else
                        {
                            DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.PvP.Spawns.Failed"));
                        }
                        break;
                    }

                case "info":
                    {
                        // Show PvP scoreboard or session infos
                        var stats = PvpManager.Instance.GetStatistics(client.Player);
                        client.Out.SendCustomTextWindow(LanguageMgr.GetTranslation(client.Account.Language, "Commands.PvP.Info.Title"), stats);
                        break;
                    }

                case "scores":
                    {
                        // Show PvP scoreboard or session infos
                        var stats = PvpManager.Instance.GetStatistics(client.Player, true);
                        client.Out.SendCustomTextWindow(LanguageMgr.GetTranslation(client.Account.Language, "Commands.PvP.Info.Title"), stats);
                        break;
                    }
            }
        }
    }
}
