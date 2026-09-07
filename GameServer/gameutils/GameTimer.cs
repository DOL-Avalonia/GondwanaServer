using System;
using System.Reflection;
using log4net;

namespace DOL.GS
{
    /// <summary>
    /// LEGACY TIMER BRIDGE (Gondwana GameLoop port).
    /// The old bucket-wheel timer and its per-region threads are gone.
    /// Every legacy GameTimer (RegionTimer, RegionTimerAction, RegionAction,
    /// DelayedCastTimer, PulsingEffectTimer, CraftAction, ...) is now a thin
    /// wrapper around an ECSGameTimer, ticked by TimerService on the GameLoop.
    /// Public API is preserved so no derived script needs to change.
    /// </summary>
    public abstract class GameTimer
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        // Kept for source compatibility (some old code references these constants).
        public static readonly long TIMER_DISABLED = long.MinValue;
        public static readonly long TIMER_RESCHEDULED = 0x40000000;

        private readonly TimeManager m_time;
        private ECSGameTimer m_inner;
        private int m_interval;
        private volatile bool m_alive;

        protected GameTimer(TimeManager time)
        {
            // Old code threw on null; we tolerate it and fall back to the shared stub.
            m_time = time ?? TimeManager.Default;
        }

        /// <summary>Repeat interval in ms. 0 = one-shot. Can be changed inside OnTick().</summary>
        public virtual int Interval
        {
            get => m_interval;
            set => m_interval = value < 0 ? 0 : value;
        }

        public long MaxInterval => int.MaxValue;

        public bool IsAlive => m_alive && m_inner is { IsAlive: true };

        /// <summary>Milliseconds until next tick, or -1 if not running.</summary>
        public int TimeUntilElapsed => (!m_alive || m_inner == null) ? -1 : m_inner.TimeUntilElapsed;

        public virtual void Start(int initialDelay)
        {
            if (initialDelay < 1)
                initialDelay = 1;

            m_alive = true;
            m_inner ??= new ECSGameTimer(null, InnerTick);
            m_inner.Start(initialDelay); // also reschedules if already running
        }

        public virtual void Stop()
        {
            m_alive = false;
            m_inner?.Stop();
        }

        public abstract void OnTick();

        private int InnerTick(ECSGameTimer timer)
        {
            if (!m_alive)
                return 0;

            try
            {
                OnTick();
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                {
                    string desc;
                    try { desc = ToString(); }
                    catch (Exception ee) { desc = GetType().FullName + " (ToString failed: " + ee.Message + ")"; }
                    log.Error("Legacy timer callback error (" + desc + ")", e);
                }

                m_alive = false;
                return 0;
            }

            if (!m_alive)
                return 0;

            // Legacy semantics: after a tick, reschedule using Interval; 0 stops (one-shot).
            return m_interval;
        }

        /// <summary>Unified monotonic clock (was a Stopwatch; now the GameLoop clock).</summary>
        public static uint GetTickCount()
        {
            return (uint)GameLoop.GameLoopTime;
        }

        public static long GetTickCountLong()
        {
            return GameLoop.GameLoopTime;
        }

        public override string ToString()
        {
            return $"{GetType().FullName} interval:{m_interval} alive:{IsAlive} manager:'{m_time.Name}'";
        }

        /// <summary>
        /// Compatibility stub. Regions still hold a TimeManager reference and call
        /// Start()/Stop()/CurrentTime on it, but it no longer owns any thread.
        /// CurrentTime is now the GameLoop clock, which unifies Region.Time,
        /// GameTimer.GetTickCount() and GameLoop.GameLoopTime into ONE clock.
        /// </summary>
        public class TimeManager
        {
            public static readonly TimeManager Default = new("GameLoop");

            private readonly string m_name;

            public TimeManager(string name)
            {
                m_name = string.IsNullOrEmpty(name) ? "GameLoop" : name;
            }

            public string Name => m_name;
            public virtual long CurrentTime => GameLoop.GameLoopTime;
            public bool Running => true;
            public int ActiveTimers => 0;
            public long InvokedCount => 0;
            public long MaxInterval => int.MaxValue;

            public virtual bool Start() => true;   // no thread anymore
            public bool Stop() => true;            // no thread anymore
            public int CountTimers() => 0;

            public string GetFormattedStackTrace()
            {
                return "(legacy timers now run on the GameLoop TimerService)";
            }

            public override string ToString()
            {
                return $"time manager:'{m_name}' (GameLoop bridge) time:{CurrentTime}";
            }
        }
    }
}