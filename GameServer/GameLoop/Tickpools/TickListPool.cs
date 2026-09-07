using System.Collections.Generic;
using System.Reflection;
using log4net;

namespace DOL.GS
{
    public sealed class TickListPool<T> : TickPool<List<T>> where T : IPooledList<T>
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        protected override List<T> CreateNew() => new();
        protected override bool IsDirty(List<T> item) => item.Count > 0;

        protected override void LogDirtyItemWarning(List<T> item)
        {
            if (log.IsWarnEnabled)
                log.Warn($"List of type '{typeof(T)}' was not cleared last tick (Count: {item.Count}) (CurrentTime: {GameLoop.GameLoopTime}).");
        }

        protected override void PrepareForUse(List<T> item) { }

        protected override void OnResetItems(int itemsInUse)
        {
            for (int i = 0; i < itemsInUse; i++)
                _pool[i].Clear();
        }
    }
}