using System;
using System.Reflection;
using log4net;

namespace DOL.GS
{
    public static class GameServiceUtils
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        public static bool ShouldTick(long tickTime)
        {
            return tickTime - GameLoop.GameLoopTime <= 0;
        }
        public static void HandleServiceException<T>(Exception exception, string serviceName, T entity, GameObject entityOwner)
            where T : class, IServiceObject
        {
            if (entity != null)
                ServiceObjectStore.Remove(entity);
            log.Error($"Critical error encountered in {serviceName} (entity: {entity}) (owner: {entityOwner})", exception);
        }
    }
}