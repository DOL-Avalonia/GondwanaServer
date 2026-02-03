using System;
using System.Threading;
using DOL.GS.ServerProperties;
using DOL.GS.PacketHandler;
using log4net;
using System.Reflection;

namespace DOL.GS
{
    /// <summary>
    /// Manages the Game Time, allowing for variable speed (faster nights).
    /// Adapted from OPENDAOC for standard DOL architectures.
    /// </summary>
    public class DayNightCycle
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private const uint DAY_DURATION_MS = 24 * 60 * 60 * 1000; // 24 hours in ms
        private const uint HALF_DAY = DAY_DURATION_MS / 2;
        private const uint QUARTER_DAY = DAY_DURATION_MS / 4;

        private const double NIGHT_INCREMENT_FACTOR = 1.25; // Night runs 25% faster
        private const int UPDATE_INTERVAL_MS = 500;

        private Timer _updateTimer;
        private long _lastTickCount;
        private readonly object _lock = new object();

        public double CurrentGameTime { get; private set; }
        public uint DayIncrement { get; private set; }
        public event EventHandler NewDayStarted;

        public void Init()
        {
            // Default start: Noon
            ChangeGameTime(Properties.WORLD_DAY_INCREMENT, 0.5);

            _updateTimer = new Timer(OnUpdateTimer, null, UPDATE_INTERVAL_MS, UPDATE_INTERVAL_MS);
        }

        public void Stop()
        {
            if (_updateTimer != null)
            {
                _updateTimer.Dispose();
                _updateTimer = null;
            }
        }

        /// <summary>
        /// Resets the time to a specific point.
        /// </summary>
        /// <param name="newDayIncrement">Speed of the day</param>
        /// <param name="startTimePercent">0.0 to 1.0 (0.5 = Noon)</param>
        public void ChangeGameTime(uint newDayIncrement, double startTimePercent)
        {
            lock (_lock)
            {
                DayIncrement = Math.Max(1, newDayIncrement);
                CurrentGameTime = (uint)(startTimePercent * DAY_DURATION_MS);
                _lastTickCount = GameTimer.GetTickCount();

                SendTimeUpdateToAll();
            }
        }

        private void OnUpdateTimer(object state)
        {
            UpdateGameTime();
        }

        /// <summary>
        /// Calculates the new game time based on how much real time passed
        /// since the last update, applying Night/Day speed factors.
        /// </summary>
        private void UpdateGameTime()
        {
            lock (_lock)
            {
                long currentTick = GameTimer.GetTickCount();
                long deltaMs = currentTick - _lastTickCount;

                if (deltaMs < 0 || deltaMs > 10000) deltaMs = UPDATE_INTERVAL_MS;

                _lastTickCount = currentTick;

                double nightIncrement = DayIncrement * NIGHT_INCREMENT_FACTOR;

                while (deltaMs > 0)
                {
                    double timeOfDay = CurrentGameTime % DAY_DURATION_MS;
                    double timeToAdd = 0;
                    double usedDelta = 0;

                    // 1. Midnight to 6 AM (Night)
                    if (timeOfDay < QUARTER_DAY)
                    {
                        double distTo6Am = QUARTER_DAY - timeOfDay;
                        double realTimeNeeded = distTo6Am / nightIncrement;

                        if (deltaMs >= realTimeNeeded) { timeToAdd = distTo6Am; usedDelta = realTimeNeeded; }
                        else { timeToAdd = deltaMs * nightIncrement; usedDelta = deltaMs; }
                    }
                    // 2. 6 AM to 6 PM (Day)
                    else if (timeOfDay < (QUARTER_DAY * 3))
                    {
                        double distTo6Pm = (QUARTER_DAY * 3) - timeOfDay;
                        double realTimeNeeded = distTo6Pm / DayIncrement;

                        if (deltaMs >= realTimeNeeded) { timeToAdd = distTo6Pm; usedDelta = realTimeNeeded; }
                        else { timeToAdd = deltaMs * DayIncrement; usedDelta = deltaMs; }
                    }
                    // 3. 6 PM to Midnight (Night)
                    else
                    {
                        double distToMidnight = DAY_DURATION_MS - timeOfDay;
                        double realTimeNeeded = distToMidnight / nightIncrement;

                        if (deltaMs >= realTimeNeeded) { timeToAdd = distToMidnight; usedDelta = realTimeNeeded; }
                        else { timeToAdd = deltaMs * nightIncrement; usedDelta = deltaMs; }
                    }

                    CurrentGameTime += timeToAdd;
                    deltaMs -= (long)usedDelta;

                    if (CurrentGameTime >= DAY_DURATION_MS)
                    {
                        CurrentGameTime -= DAY_DURATION_MS;
                        try { NewDayStarted?.Invoke(this, EventArgs.Empty); }
                        catch (Exception ex) { log.Error("Error in NewDayStarted event", ex); }
                    }
                }
            }
        }

        private void SendTimeUpdateToAll()
        {
            foreach (GameClient client in WorldMgr.GetAllPlayingClients())
            {
                if (client.Player != null && client.Player.CurrentRegion != null && client.Player.CurrentRegion.UseTimeManager)
                {
                    client.Out.SendTime();
                }
            }
        }
    }
}