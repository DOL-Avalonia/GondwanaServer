using System;
using System.Reflection;
using log4net;

namespace DOL.GS
{
    public sealed class CastingService : GameServiceBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private ServiceObjectView<CastingComponent> _view;
        public static CastingService Instance { get; } = new();

        public override void Tick()
        {
            ProcessPostedActionsParallel();
            try { _view = ServiceObjectStore.UpdateAndGetView<CastingComponent>(ServiceObjectType.CastingComponent); }
            catch (Exception e) { log.Error("UpdateAndGetView failed. Skipping this tick.", e); return; }
            _view.ExecuteForEach(TickInternal);
        }

        private static void TickInternal(CastingComponent component)
        {
            try
            {
                TickMonitor monitor = new();
                component.Tick();
                if (monitor.IsLongTick(out long elapsedMs) && log.IsWarnEnabled)
                    log.Warn($"Long CastingService tick ({elapsedMs}ms) for {component.Owner?.Name}");
            }
            catch (Exception e) { GameServiceUtils.HandleServiceException(e, nameof(CastingService), component, component.Owner); }
        }
    }
}