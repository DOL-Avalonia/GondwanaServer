using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace DOL.GS
{
    /// <summary>Decides how raw aggro is turned into a target choice. One instance per brain.</summary>
    public interface IThreatStrategy
    {
        /// <summary>Entry must be deleted from the table (dead, zoned, stealthed...).</summary>
        bool ShouldBeRemoved(GameLiving target);
        /// <summary>Base -> effective aggro (distance modulation). Also returns the distance used.</summary>
        long CalculateEffectiveAggro(long baseAggro, GameLiving target, out double distance);
        /// <summary>Entry stays in the table but cannot be a target right now (not attackable, too far, shade...).</summary>
        bool ShouldBeIgnored(GameLiving target, double distance);
        bool IsHigherThreat(long incumbentEffectiveAggro, long challengerEffectiveAggro);
        GameLiving SelectTarget(ReadOnlySpan<AggroTable.TargetCandidate> candidates);
        void OnTargetRemoved(GameLiving target) { }
    }

    /// <summary>
    /// Thread-safe aggro table with separated Base / Effective aggro.
    /// Compatibility: behaves like the old Dictionary&lt;GameLiving,long&gt; (values = Base aggro).
    /// </summary>
    public sealed class AggroTable : IDictionary<GameLiving, long>, ICollection
    {
        public const long MIN_AGGRO = 1;
        private static uint _nextId;

        private readonly IThreatStrategy _threatStrategy;
        private readonly Lock _lock = new();
        private readonly object _syncRoot = new();
        private readonly List<KeyValuePair<GameLiving, AggroAmount>> _buffer = new();
        private readonly uint _id;
        private Dictionary<GameLiving, AggroAmount> _table = new();
        private Dictionary<GameLiving, AggroAmount> _tempTable;
        private int _updating;

        public IThreatStrategy ThreatStrategy => _threatStrategy;
        /// <summary>Highest-threat living that was within attack range during the last UpdateAndGetHighestThreat.</summary>
        public GameLiving LastHighestThreatInAttackRange { get; private set; }
        public GameLiving LastSelectedTarget { get; private set; }

        public AggroTable(IThreatStrategy threatStrategy)
        {
            _threatStrategy = threatStrategy ?? throw new ArgumentNullException(nameof(threatStrategy));
            _id = Interlocked.Increment(ref _nextId);
        }

        public bool IsEmpty() { lock (_lock) return _table.Count == 0; }
        public int Count { get { lock (_lock) return _table.Count; } }

        public AddResult AddOrUpdate(GameLiving living, long amount)
        {
            if (living == null) return AddResult.Ignored;
            lock (_lock) return AddOrUpdateInternal(living, amount);
        }

        public AddResult AddIfAbsent(GameLiving living, long amount)
        {
            if (living == null) return AddResult.Ignored;
            lock (_lock) return IsInInternal(living) ? AddResult.Ignored : AddOrUpdateInternal(living, amount);
        }

        public void SetBaseAggro(GameLiving living, long amount)
        {
            if (living == null) return;
            lock (_lock)
            {
                if (_table.TryGetValue(living, out AggroAmount existing)) existing.Base = Math.Max(MIN_AGGRO, amount);
                else _table[living] = new AggroAmount(Math.Max(MIN_AGGRO, amount));
            }
        }

        /// <summary>Copies our entries into another table (only the ones it does not have yet). Deadlock-free.</summary>
        public void AddTo(AggroTable other)
        {
            if (this == other || other == null) return;
            Lock first, second;
            if (_id < other._id) { first = _lock; second = other._lock; }
            else { first = other._lock; second = _lock; }
            lock (first)
                lock (second)
                {
                    foreach (KeyValuePair<GameLiving, AggroAmount> pair in _table)
                        if (!other.IsInInternal(pair.Key))
                            other.AddOrUpdateInternal(pair.Key, pair.Value.Base);
                }
        }

        public bool Remove(GameLiving living)
        {
            if (living == null) return false;
            lock (_lock) return _table.Remove(living);
        }

        public bool IsIn(GameLiving living)
        {
            if (living == null) return false;
            lock (_lock) return IsInInternal(living);
        }

        public long GetBaseAggroAmount(GameLiving living)
        {
            if (living == null) return 0;
            lock (_lock) return _table.TryGetValue(living, out AggroAmount amount) ? amount.Base : 0;
        }

        public bool TryGetValue(GameLiving living, out long amount)
        {
            if (living != null)
            {
                lock (_lock)
                {
                    if (_table.TryGetValue(living, out AggroAmount a)) { amount = a.Base; return true; }
                }
            }
            amount = 0;
            return false;
        }

        /// <summary>Tick-pooled list (do not store). Insertion order.</summary>
        public List<GameLiving> GetUnordered(int skip = 0, int take = int.MaxValue)
        {
            List<GameLiving> list = GameLoop.GetListForTick<GameLiving>();
            if (take <= 0) return list;
            lock (_lock)
            {
                int skipped = 0, taken = 0;
                foreach (GameLiving key in _table.Keys)
                {
                    if (skipped < skip) { skipped++; continue; }
                    list.Add(key);
                    if (++taken == take) break;
                }
            }
            return list;
        }

        /// <summary>Tick-pooled list (do not store), highest effective aggro first. Uses a bounded heap (top-k), not a full sort.</summary>
        public List<GameLiving> GetOrdered(int skip = 0, int take = int.MaxValue)
        {
            List<GameLiving> list = GameLoop.GetListForTick<GameLiving>();
            if (take <= 0) return list;

            KeyValuePair<GameLiving, AggroAmount>[] buffer = null;
            try
            {
                int count;
                lock (_lock)
                {
                    count = _table.Count;
                    if (count == 0) return list;
                    buffer = ArrayPool<KeyValuePair<GameLiving, AggroAmount>>.Shared.Rent(count);
                    ((ICollection<KeyValuePair<GameLiving, AggroAmount>>)_table).CopyTo(buffer, 0);
                }

                int start = Math.Min(skip, count);
                int end = (int)Math.Min((long)skip + take, count);
                if (start >= end) return list;

                PriorityQueue<GameLiving, long> heap = new(end);
                for (int i = 0; i < count; i++)
                {
                    long effective = buffer[i].Value.Effective;
                    if (heap.Count < end) heap.Enqueue(buffer[i].Key, effective);
                    else if (heap.TryPeek(out _, out long minEffective) && effective > minEffective) heap.EnqueueDequeue(buffer[i].Key, effective);
                }

                int resultCount = end - start;
                CollectionsMarshal.SetCount(list, resultCount);
                for (int i = 0; i < resultCount; i++)
                    list[resultCount - 1 - i] = heap.Dequeue();
                return list;
            }
            finally
            {
                if (buffer != null) ArrayPool<KeyValuePair<GameLiving, AggroAmount>>.Shared.Return(buffer, true);
            }
        }

        public List<(GameLiving Living, long BaseAggro, long EffectiveAggro)> GetDebug()
        {
            lock (_lock)
                return _table.OrderByDescending(static x => x.Value.Effective).Select(static x => (x.Key, x.Value.Base, x.Value.Effective)).ToList();
        }

        public bool SetTemporaryAggroTable()
        {
            lock (_lock)
            {
                if (_tempTable != null) return false;
                _tempTable = _table;
                _table = new();
                return true;
            }
        }

        public bool UnsetTemporaryAggroTable()
        {
            lock (_lock)
            {
                if (_tempTable == null) return false;
                _table = _tempTable;
                _tempTable = null;
                return true;
            }
        }

        /// <summary>
        /// One pass over the table: purges invalid entries, recomputes Effective aggro, remembers the best target
        /// within attackRange and returns the target chosen by the strategy.
        /// </summary>
        public GameLiving UpdateAndGetHighestThreat(int attackRange)
        {
            if (Interlocked.Exchange(ref _updating, 1) != 0)
                return LastSelectedTarget; // concurrent call: keep last decision instead of throwing

            try
            {
                lock (_lock)
                {
                    _buffer.Clear();
                    _buffer.AddRange(_table);
                }

                if (_buffer.Count == 0)
                {
                    LastHighestThreatInAttackRange = null;
                    LastSelectedTarget = null;
                    return null;
                }

                TargetCandidate[] candidates = ArrayPool<TargetCandidate>.Shared.Rent(_buffer.Count);
                int candidateCount = 0;

                try
                {
                    GameLiving highestInRange = null;
                    long highestInRangeAggro = -1;

                    for (int i = 0; i < _buffer.Count; i++)
                    {
                        GameLiving living = _buffer[i].Key;
                        AggroAmount amount = _buffer[i].Value;

                        if (_threatStrategy.ShouldBeRemoved(living))
                        {
                            RemoveAndNotify(living);
                            continue;
                        }

                        long effective = _threatStrategy.CalculateEffectiveAggro(amount.Base, living, out double distance);

                        if (_threatStrategy.ShouldBeIgnored(living, distance))
                        {
                            amount.Effective = 0;
                            continue;
                        }

                        amount.Effective = effective;

                        if (distance <= attackRange && _threatStrategy.IsHigherThreat(highestInRangeAggro, effective))
                        {
                            highestInRangeAggro = effective;
                            highestInRange = living;
                        }

                        candidates[candidateCount++] = new TargetCandidate(living, effective, distance);
                    }

                    _buffer.Clear();
                    LastHighestThreatInAttackRange = highestInRange;
                    GameLiving selected = candidateCount > 0 ? _threatStrategy.SelectTarget(candidates.AsSpan(0, candidateCount)) : null;
                    LastSelectedTarget = selected;
                    return selected;
                }
                finally
                {
                    ArrayPool<TargetCandidate>.Shared.Return(candidates, true);
                }
            }
            finally
            {
                Volatile.Write(ref _updating, 0);
            }
        }

        /// <summary>Cheap "who, among my aggro list, is within range of origin and has the most base aggro".</summary>
        public GameLiving FindHighestBaseAggroWithin(GameLiving origin, int range, Func<GameLiving, bool> filter = null)
        {
            if (origin == null) return null;
            KeyValuePair<GameLiving, AggroAmount>[] buffer;
            int count;
            lock (_lock)
            {
                count = _table.Count;
                if (count == 0) return null;
                buffer = ArrayPool<KeyValuePair<GameLiving, AggroAmount>>.Shared.Rent(count);
                ((ICollection<KeyValuePair<GameLiving, AggroAmount>>)_table).CopyTo(buffer, 0);
            }
            try
            {
                GameLiving best = null;
                long bestAggro = 0;
                for (int i = 0; i < count; i++)
                {
                    long aggro = buffer[i].Value.Base;
                    if (aggro <= bestAggro) continue;
                    GameLiving living = buffer[i].Key;
                    if (!origin.IsWithinRadius(living, range)) continue;
                    if (filter != null && !filter(living)) continue;
                    best = living;
                    bestAggro = aggro;
                }
                return best;
            }
            finally
            {
                ArrayPool<KeyValuePair<GameLiving, AggroAmount>>.Shared.Return(buffer, true);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _table.Clear();
                LastHighestThreatInAttackRange = null;
                LastSelectedTarget = null;
            }
        }

        public Dictionary<GameLiving, long> ToDictionary()
        {
            lock (_lock)
            {
                Dictionary<GameLiving, long> result = new(_table.Count);
                foreach (KeyValuePair<GameLiving, AggroAmount> pair in _table)
                    result[pair.Key] = pair.Value.Base;
                return result;
            }
        }

        private void RemoveAndNotify(GameLiving living)
        {
            bool removed;
            lock (_lock) removed = _table.Remove(living);
            if (removed) _threatStrategy.OnTargetRemoved(living);
        }

        private AddResult AddOrUpdateInternal(GameLiving living, long amount)
        {
            bool isFirst = _table.Count == 0;
            if (_table.TryGetValue(living, out AggroAmount aggroAmount))
            {
                aggroAmount.Base = Math.Max(MIN_AGGRO, aggroAmount.Base + amount);
                return AddResult.Updated;
            }
            _table[living] = new AggroAmount(Math.Max(MIN_AGGRO, amount));
            return isFirst ? AddResult.First : AddResult.New;
        }

        private bool IsInInternal(GameLiving living) => _table.ContainsKey(living);

        // ---------- Dictionary<GameLiving,long> compatibility surface ----------
        public long this[GameLiving living] { get => GetBaseAggroAmount(living); set => SetBaseAggro(living, value); }
        public ICollection<GameLiving> Keys { get { lock (_lock) return new List<GameLiving>(_table.Keys); } }
        public ICollection<long> Values
        {
            get
            {
                lock (_lock)
                {
                    List<long> values = new(_table.Count);
                    foreach (AggroAmount a in _table.Values) values.Add(a.Base);
                    return values;
                }
            }
        }
        public bool IsReadOnly => false;
        public bool ContainsKey(GameLiving living) => IsIn(living);
        /// <summary>Compatibility: adds aggro (cumulative), does not throw if already present.</summary>
        public void Add(GameLiving living, long amount) => AddOrUpdate(living, amount);
        void ICollection<KeyValuePair<GameLiving, long>>.Add(KeyValuePair<GameLiving, long> item) => AddOrUpdate(item.Key, item.Value);
        bool ICollection<KeyValuePair<GameLiving, long>>.Contains(KeyValuePair<GameLiving, long> item) => TryGetValue(item.Key, out long v) && v == item.Value;
        bool ICollection<KeyValuePair<GameLiving, long>>.Remove(KeyValuePair<GameLiving, long> item) => Remove(item.Key);
        void ICollection<KeyValuePair<GameLiving, long>>.CopyTo(KeyValuePair<GameLiving, long>[] array, int arrayIndex)
        {
            foreach (KeyValuePair<GameLiving, long> pair in ToDictionary()) array[arrayIndex++] = pair;
        }
        public IEnumerator<KeyValuePair<GameLiving, long>> GetEnumerator() => ToDictionary().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        bool ICollection.IsSynchronized => true;
        object ICollection.SyncRoot => _syncRoot;
        void ICollection.CopyTo(Array array, int index)
        {
            foreach (KeyValuePair<GameLiving, long> pair in ToDictionary()) array.SetValue(pair, index++);
        }

        public enum AddResult { Ignored, First, New, Updated }

        public readonly record struct TargetCandidate(GameLiving Living, long EffectiveAggro, double Distance);

        private sealed class AggroAmount
        {
            public long Base;
            public long Effective;
            public AggroAmount(long baseAggro) { Base = baseAggro; }
        }
    }
}