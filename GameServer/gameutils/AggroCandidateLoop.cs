using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DOL.GS
{
    /// <summary>
    /// Zero-allocation round-robin enumerator used by brains when scanning for targets.
    /// It stops as soon as the caller has spent its LOS budget for this tick (checksThisTick >= maxChecks)
    /// and remembers where it stopped (index) so the next Think() resumes with the next candidates.
    /// </summary>
    public ref struct AggroCandidateLoop<T>
    {
        private readonly ReadOnlySpan<T> _targets;
        private readonly int _maxChecks;
        private ref int _checksThisTick;
        private ref int _index;
        private int _processed;

        public T Current { get; private set; }
        public int Remaining => _targets.Length - _processed;

        public AggroCandidateLoop(List<T> targets, int maxChecks, ref int checksThisTick, ref int index)
            : this(targets != null ? CollectionsMarshal.AsSpan(targets) : default, maxChecks, ref checksThisTick, ref index) { }

        public AggroCandidateLoop(ReadOnlySpan<T> targets, int maxChecks, ref int checksThisTick, ref int index)
        {
            _targets = targets;
            _maxChecks = maxChecks;
            _checksThisTick = ref checksThisTick;
            _index = ref index;
            _processed = 0;
            Current = default;
        }

        public readonly AggroCandidateLoop<T> GetEnumerator() => this;

        public bool MoveNext()
        {
            if (_targets.Length == 0 || _processed >= _targets.Length || _checksThisTick >= _maxChecks)
            {
                Current = default;
                return false;
            }
            Current = _targets[(_index + _processed) % _targets.Length];
            _processed++;
            return true;
        }

        public void Dispose()
        {
            if (_targets.Length > 0)
                _index = (_index + _processed) % _targets.Length;
        }
    }
}