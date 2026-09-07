using System;
using System.Collections.Generic;
using System.Threading;

namespace DOL.GS
{
    // Thread-safe, drain-style pending list (simple lock-based implementation).
    public sealed class DrainArray<T>
    {
        private List<T> _items = new();
        private List<T> _spare = new();
        private readonly Lock _lock = new();
        public bool Any
        {
            get { lock (_lock) return _items.Count > 0; }
        }
        public void Add(T item)
        {
            lock (_lock) _items.Add(item);
        }
        public void DrainTo<TContext>(Action<T, TContext> action, TContext context)
        {
            List<T> toDrain;
            lock (_lock)
            {
                if (_items.Count == 0)
                    return;
                toDrain = _items;
                _items = _spare;
                _spare = null;
            }
            foreach (T item in toDrain)
                action(item, context);
            toDrain.Clear();
            lock (_lock) _spare = toDrain;
        }
    }
}