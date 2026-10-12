using System;
using System.Collections.Generic;
using System.Threading;

namespace DOL.GS
{
    /// <summary>
    /// Multi-producer buffer drained in batches.
    /// Safe for: Add from any thread, Add while a drain is running (goes to the next batch),
    /// concurrent or nested drains, and exceptions thrown by the drain action.
    /// </summary>
    public sealed class DrainArray<T>
    {
        private readonly Lock _lock = new();
        private List<T> _items = new();
        private List<T> _spare = new();

        public bool Any
        {
            get
            {
                lock (_lock)
                    return _items.Count > 0;
            }
        }

        public void Add(T item)
        {
            lock (_lock)
                _items.Add(item);
        }

        public void DrainTo(Action<T> action)
        {
            DrainTo(static (item, act) => act(item), action);
        }

        public void DrainTo<TState>(Action<T, TState> action, TState state)
        {
            List<T> toDrain;

            lock (_lock)
            {
                if (_items.Count == 0)
                    return;

                toDrain = _items;
                _items = _spare ?? new List<T>(Math.Max(16, toDrain.Count));
                _spare = null;
            }

            try
            {
                for (int i = 0; i < toDrain.Count; i++)
                    action(toDrain[i], state);
            }
            finally
            {
                toDrain.Clear();

                lock (_lock)
                {
                    if (_spare == null)
                        _spare = toDrain;
                }
            }
        }
    }
}