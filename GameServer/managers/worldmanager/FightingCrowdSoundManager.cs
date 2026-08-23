using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;
using log4net;

namespace AmteScripts.Managers
{
    public class FightingCrowdSoundManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private static Timer _timer;
        private static readonly Random _random = new Random();

        private const string LAST_CROWD_SOUND_PROP = "LastCrowdSoundTick";
        private const long CROWD_SOUND_COOLDOWN = 9400;
        private const ushort CROWD_RADIUS = 2000;
        private const int MIN_PLAYERS_IN_COMBAT = 18;
        private static readonly ushort[] CROWD_SOUNDS = { 1567, 1568, 1569 };

        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            _timer = new Timer(CheckCrowdSound, null, 5000, Timeout.Infinite);
            log.Info("FightingCrowdSoundManager initialized.");
        }

        [ScriptUnloadedEvent]
        public static void OnScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }

        private static void CheckCrowdSound(object state)
        {
            try
            {
                var allClients = WorldMgr.GetAllPlayingClients();
                long currentTime = GameTimer.GetTickCount();

                foreach (GameClient client in allClients)
                {
                    GamePlayer player = client.Player;

                    if (player == null || player.ObjectState != GameObject.eObjectState.Active || !player.IsAlive || player.CurrentRegion == null)
                        continue;

                    long lastSoundTick = player.TempProperties.getProperty<long>(LAST_CROWD_SOUND_PROP, 0L);

                    if (lastSoundTick != 0L && currentTime - lastSoundTick < CROWD_SOUND_COOLDOWN)
                        continue;

                    if (!player.InCombat)
                        continue;

                    // PvP/RvR Area Check - GameServer.ServerRules.IsInPvPArea handles RvR, Battlegrounds, PvP Sessions, Territories, and Banners natively!
                    if (!GameServer.ServerRules.IsInPvPArea(player))
                        continue;

                    // Gather players in 2000 radius
                    List<GamePlayer> playersInRadius = new List<GamePlayer>();
                    int combatCount = 1;
                    playersInRadius.Add(player);

 
                    IEnumerable playersAround = player.CurrentRegion.GetPlayersInRadius(player.Coordinate, CROWD_RADIUS, false, false);

                    foreach (GamePlayer p in playersAround)
                    {
                        if (p != null && p != player && p.IsAlive && p.ObjectState == GameObject.eObjectState.Active)
                        {
                            playersInRadius.Add(p);

                            // Non-combat players won't count
                            if (p.InCombat)
                            {
                                combatCount++;
                            }
                        }
                    }

                    // Trigger the sound for EVERYONE eligible in that radius
                    if (combatCount >= MIN_PLAYERS_IN_COMBAT)
                    {
                        ushort randomSoundId;
                        lock (_random)
                        {
                            randomSoundId = CROWD_SOUNDS[_random.Next(CROWD_SOUNDS.Length)];
                        }

                        foreach (GamePlayer p in playersInRadius)
                        {
                            long pLastTick = p.TempProperties.getProperty<long>(LAST_CROWD_SOUND_PROP, 0L);

                            if ((pLastTick == 0L || currentTime - pLastTick >= CROWD_SOUND_COOLDOWN) && GameServer.ServerRules.IsInPvPArea(p))
                            {
                                p.Out.SendSoundEffect(randomSoundId, p.Position, 0);
                                p.TempProperties.setProperty(LAST_CROWD_SOUND_PROP, currentTime);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                    log.Error("Error in FightingCrowdSoundManager: ", ex);
            }
            finally
            {
                if (_timer != null)
                {
                    _timer.Change(5000, Timeout.Infinite);
                }
            }
        }
    }
}