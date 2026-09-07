using DOL.Timing;

namespace DOL.GS
{
    public readonly struct TickMonitor
    {
        private readonly long _startTime;

        public TickMonitor()
        {
            _startTime = MonotonicTime.NowMs;
        }

        public bool IsLongTick(out long elapsedMs)
        {
            elapsedMs = MonotonicTime.NowMs - _startTime;
            return elapsedMs > ServerProperties.Properties.GAME_LOOP_LONG_TICK_THRESHOLD_MS;
        }
    }
}