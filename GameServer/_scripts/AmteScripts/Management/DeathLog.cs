using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using AmteScripts.Managers;
using DOL.Database;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.Language;
using GameServerScripts.Amtescripts.Managers;
using log4net;


namespace DOL.GS.GameEvents
{
    public static class DeathLog
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        
        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.AddHandler(GameLivingEvent.Dying, new DOLEventHandler(LivingKillEnnemy));

            log.Info("DeathLog initialized");
        }

        [ScriptUnloadedEvent]
        public static void OnScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(GameLivingEvent.Dying, new DOLEventHandler(LivingKillEnnemy));
        }

        private static bool IsDuelKill(GamePlayer killer, GamePlayer victim)
        {
            return (killer != null && killer.DuelTarget == victim) || (victim != null && victim.DuelTarget == killer);
        }

        public static void LivingKillEnnemy(DOLEvent e, object sender, EventArgs args)
        {
            if (args is not DyingEventArgs { Killer: not null } dyingArgs)
                return;
            
            var killer = dyingArgs.Killer;
            if (sender is GamePlayer playerVictim)
            {
                if (killer is GamePlayer playerKiller)
                {
                    // Ignore kills on Duels
                    if (IsDuelKill(playerKiller, playerVictim))
                    {
                        return;
                    }

                    // Ignore kills on Outlaws
                    if (playerVictim.Reputation < 0)
                    {
                        return;
                    }
                    
                    // If killer is GM, let go
                    if (playerKiller.Client.Account.PrivLevel > 1)
                    {
                        // Unless
                        if (ServerProperties.Properties.ENABLE_DEBUG)
                        {
                            playerKiller.SendMessage("The server is in DEBUG mode so you will be counted as a player killer!", eChatType.CT_Important);
                        }
                        else
                        {
                            return;
                        }
                    }

                    // Damned players are allowed to kill players
                    if (playerKiller.IsDamned)
                    {
                        return;
                    }

                    // PvP area
                    if (GameServer.ServerRules.IsInPvPArea(playerVictim))
                    {
                        return;
                    }

                    bool nearRelicCarrier = TerritoryRelicManager.ActiveRelics.Values
                        .Any(r => r.CurrentCarrier != null
                               && r.CurrentCarrier.CurrentRegionID == playerVictim.CurrentRegionID
                               && r.CurrentCarrier.Coordinate.DistanceTo(playerVictim.Coordinate) <= 3000);

                    bool nearPhysicalRelic = TerritoryRelicManager.ActiveRelics.Values
                        .Any(r => r.CurrentCarrier == null
                               && r.ObjectState == GameObject.eObjectState.Active
                               && r.CurrentRegionID == playerVictim.CurrentRegionID
                               && r.Coordinate.DistanceTo(playerVictim.Coordinate) <= 2500);

                    if (nearRelicCarrier || nearPhysicalRelic)
                    {
                        return;
                    }

                    bool autoReport = DeathCheck.Instance.IsChainKiller(playerKiller, playerVictim);
                    if (autoReport)
                    {
                        // Automatically report player
                        --playerKiller.Reputation;
                        if (!playerKiller.Wanted)
                        {
                            playerKiller.Wanted = true;
                        }
                        GameServer.Database.AddObject(new DBDeathLog(playerVictim, playerKiller, true));
                        playerKiller.Out.SendMessage(LanguageMgr.GetTranslation(playerKiller.Client, "GameObjects.GamePlayer.Multiplekills", playerKiller.GetPersonalizedName(playerVictim)), PacketHandler.eChatType.CT_YouDied, PacketHandler.eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        GameServer.Database.AddObject(new DBDeathLog(playerVictim, playerKiller, false));
                    }
                }
            }
            else if (sender is GuardNPC)
            {
                if (killer is GamePlayer playerKiller)
                {
                    if (!GameServer.ServerRules.IsInPvPArea(playerKiller))
                    {
                        if (playerKiller.Group != null)
                        {
                            foreach (GamePlayer groupPlayer in playerKiller.Group.GetMembersInTheGroup())
                            {
                                // I've copy pasted this from the code that was there before, but this seems grief-prone
                                GuardKillLostReputation(groupPlayer);
                            }
                        }
                        else
                        {
                            GuardKillLostReputation(playerKiller);
                        }
                    }
                }
            }
        }

        private static void GuardKillLostReputation(GamePlayer player)
        {
            if (player.Client.Account.PrivLevel <= 1)
            {
                player.Reputation--;
                player.SaveIntoDatabase();
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameObjects.GamePlayer.GuardKill"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
            }
        }
    }
}
