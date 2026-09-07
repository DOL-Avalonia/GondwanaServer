using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;

namespace DOL.GS
{
    public sealed class RelocationService : GameServiceBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private const int RELOCATION_INTERVAL = 200;

        private long _nextTick;
        private readonly List<Region> _regions = new();

        public static RelocationService Instance { get; } = new();

        public override void Tick()
        {
            ProcessPostedActions();

            if (!GameServiceUtils.ShouldTick(_nextTick))
                return;
            _nextTick = GameLoop.GameLoopTime + RELOCATION_INTERVAL;

            _regions.Clear();
            _regions.AddRange(WorldMgr.GetAllRegions());
            GameLoop.ExecuteForEach(_regions, _regions.Count, RelocateRegion);
        }

        private static void RelocateRegion(Region region)
        {
            try
            {
                // Same condition as the old dedicated thread.
                if (region.NumPlayers > 0
                    && (region.LastRelocationTime + Zone.MAX_REFRESH_INTERVAL) * 10 * 1000 < DateTime.Now.Ticks)
                    region.Relocate();
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error($"RelocateRegion failed (Region: {region.ID})", e);
            }
        }
    }
}