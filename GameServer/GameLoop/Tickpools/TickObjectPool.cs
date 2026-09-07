using System.Reflection;
using log4net;

namespace DOL.GS
{
    public sealed class TickObjectPool<T> : TickPool<T> where T : IPooledObject<T>, new()
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        protected override T CreateNew() => new T();
        protected override bool IsDirty(T item) => item.IssuedTimestamp != 0;

        protected override void LogDirtyItemWarning(T item)
        {
            if (log.IsWarnEnabled)
                log.Warn($"Item '{item}' was not released last tick (IssuedTimestamp: {item.IssuedTimestamp}) (CurrentTime: {GameLoop.GameLoopTime}).");
        }

        protected override void PrepareForUse(T item) => item.IssuedTimestamp = GameLoop.GameLoopTime;
        protected override void OnResetItems(int itemsInUse) { }
    }
}