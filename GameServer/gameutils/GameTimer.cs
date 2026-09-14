using System;
using System.Reflection;
using log4net;

namespace DOL.GS
{
    /// <summary>
    /// Legacy timer base class. Since the GameLoop migration this class no longer
    /// owns any thread: every GameTimer — and therefore every RegionTimer,
    /// RegionAction, RegionTimerAction and PulsingEffectTimer — is executed by
    /// TimerService on the game loop, through an internal ECSGameTimer.
    ///
    /// The nested TimeManager survives only as a compile shim for Region/WorldMgr:
    /// it starts no thread and simply exposes the game loop clock. This means
    /// Region.Time, GameTimer.GetTickCount() and ECSGameTimer all share ONE clock.
    ///
    /// Phase 2 goal: migrate call sites to ECSGameTimer directly, then delete
    /// this file together with RegionTimer/RegionAction/RegionTimerAction.
    /// </summary>
    public abstract class GameTimer
    {
        private readonly InnerTimer _inner;
        private int _interval;

        /// <param name="time">Kept for source compatibility. Ignored: all timers run on the GameLoop.</param>
        protected GameTimer(TimeManager time)
        {
            _inner = new InnerTimer(this);
        }

        /// <summary>
        /// Legacy repeat interval. OnTick implementations may change it;
        /// a value of 0 stops the timer after the current tick.
        /// </summary>
        public virtual int Interval
        {
            get => _interval;
            set => _interval = value;
        }

        /// <summary>Legacy property, no longer meaningful. Kept for compatibility.</summary>
        public long MaxInterval => int.MaxValue;

        public bool IsAlive => _inner.IsAlive;

        /// <summary>Milliseconds until the next tick, or -1 when stopped.</summary>
        public int TimeUntilElapsed => _inner.IsAlive ? _inner.TimeUntilElapsed : -1;

        /// <summary>
        /// Legacy semantics preserved: the first tick fires after 'initialDelay',
        /// then the timer repeats every 'Interval' ms (0 = one-shot).
        /// </summary>
        public virtual void Start(int initialDelay)
        {
            if (initialDelay < 1)
                initialDelay = 1;
            _inner.Start(initialDelay);
        }

        public virtual void Stop()
        {
            _inner.Stop();
        }

        public abstract void OnTick();

        public override string ToString() => $"{GetType().FullName} interval:{_interval}";

        /// <summary>
        /// Unified monotonic clock (was a Stopwatch; now the GameLoop clock).
        /// </summary>
        public static uint GetTickCount()
        {
            return (uint)GameLoop.GameLoopTime;
        }

        public static long GetTickCountLong()
        {
            return GameLoop.GameLoopTime;
        }

        /// <summary>
        /// The bridge that actually runs on TimerService.
        /// </summary>
        private sealed class InnerTimer : ECSGameTimer
        {
            private readonly GameTimer _owner;

            public InnerTimer(GameTimer owner) : base(null)
            {
                _owner = owner;
                Callback = Tick;
            }

            private int Tick(ECSGameTimer timer)
            {
                _owner.OnTick();
                // Honor Stop() called from inside OnTick (e.g. PulsingEffectTimer),
                // otherwise repeat using the legacy Interval contract.
                return IsAlive ? _owner.Interval : 0;
            }

            public override string ToString() => _owner.ToString();
        }

        /// <summary>
        /// Compile shim for Region.cs / WorldMgr.cs. No thread, no buckets.
        /// Region.StartRegionMgr()/StopRegionMgr() become no-ops and Region.Time
        /// returns the game loop clock.
        /// </summary>
        public class TimeManager
        {
            public TimeManager(string name) { Name = name; }

            public string Name { get; }
            public bool Running { get; private set; }
            public long CurrentTime => GameLoop.GameLoopTime;
            public int ActiveTimers => 0;
            public long InvokedCount => 0;

            public virtual bool Start()
            {
                Running = true;
                return true;
            }

            public bool Stop()
            {
                Running = false;
                return true;
            }

            public string GetFormattedStackTrace()
            {
                return "(all timers run on the GameLoop)";
            }
        }
    }
}