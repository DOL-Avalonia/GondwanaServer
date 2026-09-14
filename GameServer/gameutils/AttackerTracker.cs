using System;
using System.Collections.Generic;
using System.Threading;

namespace DOL.GS
{
    /// <summary>
    /// Replaces GameLiving.m_attackers (List&lt;GameObject&gt;).
    /// - Thread-safe, self-cleaning (1s timer only while it has something to clean).
    /// - Strict melee counter (used by evade/parry/block penalties). Ranged / spell attackers do not count.
    /// - Two horizons per attacker: "active" (ExpireTime = one attack interval) and "remembered"
    ///   (ForgetTime = ATTACKER_MEMORY_DURATION) so quests/tasks/kill credit still see everyone who fought.
    /// - Interrupt bookkeeping (who interrupted last, until when).
    /// </summary>
    public sealed class AttackerTracker
    {
        public const int DEFAULT_ATTACKER_LIFETIME = 10000; // legacy AddAttacker(GameObject) without AttackData
        public const int MIN_ATTACKER_LIFETIME = 2500;      // never expire faster than this (avoids flapping between swings)
        public const int ATTACKER_MEMORY_DURATION = 10000;  // kept for kill credit after the attacker stopped attacking
        private const int CHECK_INTERVAL = 1000;

        private readonly GameLiving _owner;
        private readonly Lock _lock = new();
        private readonly Dictionary<GameLiving, AttackerInfo> _attackers = new();
        private readonly Stack<AttackerInfo> _infoPool = new();
        private readonly AttackerCheckTimer _checkTimer;

        private int _meleeAttackerCount;
        private GameLiving _lastInterrupter;
        private long _interruptExpireTime;
        private long _selfInterruptExpireTime;

        public AttackerTracker(GameLiving owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _checkTimer = new AttackerCheckTimer(this);
        }

        public GameLiving Owner => _owner;

        /// <summary>Everyone remembered (active or recently active).</summary>
        public int Count { get { lock (_lock) return _attackers.Count; } }

        /// <summary>Attackers currently swinging melee weapons at us. Lock-free.</summary>
        public int MeleeCount => Volatile.Read(ref _meleeAttackerCount);

        public bool HasAttackers => Count > 0;
        public bool HasMeleeAttackers => MeleeCount > 0;
        public long InterruptExpireTime => Volatile.Read(ref _interruptExpireTime);
        public GameLiving LastInterrupter { get { lock (_lock) return _lastInterrupter; } }

        /// <summary>
        /// Tick-pooled list: iterate it immediately, never store it (it is recycled next game loop tick).
        /// </summary>
        public List<GameLiving> Attackers
        {
            get
            {
                List<GameLiving> result = GameLoop.GetListForTick<GameLiving>();
                CopyAttackersTo(result);
                return result;
            }
        }

        /// <summary>Allocated copy, safe to keep.</summary>
        public List<GameLiving> GetAttackersSnapshot()
        {
            lock (_lock)
            {
                List<GameLiving> result = new(_attackers.Count);
                foreach (GameLiving key in _attackers.Keys)
                    result.Add(key);
                return result;
            }
        }

        /// <summary>Allocated copy typed as GameObject, for the legacy GameLiving.Attackers property.</summary>
        public List<GameObject> GetAttackersAsGameObjects()
        {
            lock (_lock)
            {
                List<GameObject> result = new(_attackers.Count);
                foreach (GameLiving key in _attackers.Keys)
                    result.Add(key);
                return result;
            }
        }

        public void CopyAttackersTo(List<GameLiving> destination)
        {
            lock (_lock)
            {
                destination.EnsureCapacity(destination.Count + _attackers.Count);
                foreach (GameLiving key in _attackers.Keys)
                    destination.Add(key);
            }
        }

        public void AddOrUpdate(GameLiving attacker, bool isMelee, long expireTime)
        {
            if (attacker == null || attacker == _owner)
                return;

            long now = GameLoop.GameLoopTime;
            if (expireTime < now + MIN_ATTACKER_LIFETIME)
                expireTime = now + MIN_ATTACKER_LIFETIME;
            long forgetTime = Math.Max(expireTime, now + ATTACKER_MEMORY_DURATION);
            bool wakeUp = false;

            lock (_lock)
            {
                if (_attackers.TryGetValue(attacker, out AttackerInfo info))
                {
                    info.IsMelee = isMelee;
                    if (expireTime > info.ExpireTime) info.ExpireTime = expireTime;
                    if (forgetTime > info.ForgetTime) info.ForgetTime = forgetTime;
                }
                else
                {
                    info = RentInfoUnsafe();
                    info.IsMelee = isMelee;
                    info.ExpireTime = expireTime;
                    info.ForgetTime = forgetTime;
                    info.CountedAsMelee = false;
                    _attackers.Add(attacker, info);
                    wakeUp = true;
                }

                UpdateMeleeCounterUnsafe(info, now);
            }

            if (wakeUp)
                _checkTimer.WakeUp();
        }

        public bool Remove(GameLiving attacker)
        {
            if (attacker == null) return false;
            lock (_lock)
            {
                if (!_attackers.Remove(attacker, out AttackerInfo info)) return false;
                if (info.CountedAsMelee) _meleeAttackerCount--;
                ReturnInfoUnsafe(info);
                return true;
            }
        }

        public bool ContainsAttacker(GameLiving attacker)
        {
            if (attacker == null) return false;
            lock (_lock) return _attackers.ContainsKey(attacker);
        }

        public bool ContainsAttacker(GameObject attacker) => attacker is GameLiving living && ContainsAttacker(living);

        /// <summary>True if the attacker is still inside its "active" window (one attack interval).</summary>
        public bool IsActiveAttacker(GameLiving attacker)
        {
            if (attacker == null) return false;
            lock (_lock) return _attackers.TryGetValue(attacker, out AttackerInfo info) && info.ExpireTime > GameLoop.GameLoopTime;
        }

        public bool TryGetInfo(GameLiving attacker, out bool isMelee, out long expireTime)
        {
            isMelee = false;
            expireTime = 0;
            if (attacker == null) return false;
            lock (_lock)
            {
                if (!_attackers.TryGetValue(attacker, out AttackerInfo info)) return false;
                isMelee = info.IsMelee;
                expireTime = info.ExpireTime;
                return true;
            }
        }

        // ---------------- interrupts ----------------

        public void SetInterrupt(GameLiving interrupter, long expireTime)
        {
            if (interrupter == null || interrupter == _owner)
            {
                SetSelfInterrupt(expireTime);
                return;
            }

            bool wakeUp;
            lock (_lock)
            {
                if (_interruptExpireTime >= expireTime) return;
                _lastInterrupter = interrupter;
                _interruptExpireTime = expireTime;
                wakeUp = true;
            }
            if (wakeUp) _checkTimer.WakeUp();
        }

        public void SetSelfInterrupt(long expireTime)
        {
            lock (_lock)
            {
                if (_selfInterruptExpireTime < expireTime)
                    _selfInterruptExpireTime = expireTime;
            }
        }

        public bool IsInterrupted(out GameLiving lastInterrupter)
        {
            lastInterrupter = null;
            lock (_lock)
            {
                if (_interruptExpireTime <= GameLoop.GameLoopTime) return false;
                lastInterrupter = _lastInterrupter;
                return true;
            }
        }

        public bool IsSelfInterrupted() => Volatile.Read(ref _selfInterruptExpireTime) > GameLoop.GameLoopTime;
        public bool IsInterruptedOrSelfInterrupted() => IsInterrupted(out _) || IsSelfInterrupted();

        public long GetInterruptRemainingDuration()
        {
            long expire = Math.Max(Volatile.Read(ref _selfInterruptExpireTime), Volatile.Read(ref _interruptExpireTime));
            return Math.Max(0, expire - GameLoop.GameLoopTime);
        }

        // ---------------- maintenance ----------------

        public void Clear()
        {
            lock (_lock) ClearUnsafe();
            _checkTimer.Stop();
        }

        private void ClearUnsafe()
        {
            foreach (AttackerInfo info in _attackers.Values)
                ReturnInfoUnsafe(info);
            _attackers.Clear();
            _meleeAttackerCount = 0;
            _lastInterrupter = null;
            _interruptExpireTime = 0;
            _selfInterruptExpireTime = 0;
        }

        private void UpdateMeleeCounterUnsafe(AttackerInfo info, long now)
        {
            bool shouldCount = info.IsMelee && info.ExpireTime > now;
            if (shouldCount && !info.CountedAsMelee) { info.CountedAsMelee = true; _meleeAttackerCount++; }
            else if (!shouldCount && info.CountedAsMelee) { info.CountedAsMelee = false; _meleeAttackerCount--; }
        }

        private void ExpireUnsafe(long now)
        {
            // Dictionary.Remove during enumeration is allowed since .NET Core 3.0.
            foreach (KeyValuePair<GameLiving, AttackerInfo> pair in _attackers)
            {
                AttackerInfo info = pair.Value;
                if (info.ForgetTime <= now)
                {
                    _attackers.Remove(pair.Key);
                    if (info.CountedAsMelee) _meleeAttackerCount--;
                    ReturnInfoUnsafe(info);
                    continue;
                }
                if (info.CountedAsMelee && info.ExpireTime <= now)
                {
                    info.CountedAsMelee = false;
                    _meleeAttackerCount--;
                }
            }
        }

        private bool TryClearInterruptUnsafe(long now)
        {
            if (_interruptExpireTime > now) return false;
            _lastInterrupter = null;
            return true;
        }

        private AttackerInfo RentInfoUnsafe() => _infoPool.Count > 0 ? _infoPool.Pop() : new AttackerInfo();
        private void ReturnInfoUnsafe(AttackerInfo info) { if (_infoPool.Count < 16) _infoPool.Push(info); }

        private sealed class AttackerInfo
        {
            public bool IsMelee;
            public long ExpireTime;
            public long ForgetTime;
            public bool CountedAsMelee;
        }

        private sealed class AttackerCheckTimer : ECSGameTimerWrapperBase
        {
            private readonly AttackerTracker _tracker;

            public AttackerCheckTimer(AttackerTracker tracker) : base(tracker._owner)
            {
                _tracker = tracker;
                Interval = CHECK_INTERVAL;
            }

            public void WakeUp()
            {
                if (IsAlive) return;
                Start(CHECK_INTERVAL);
            }

            protected override int OnTick(ECSGameTimer timer)
            {
                long now = GameLoop.GameLoopTime;
                lock (_tracker._lock)
                {
                    if (_tracker._owner.ObjectState != GameObject.eObjectState.Active)
                    {
                        _tracker.ClearUnsafe();
                        return 0;
                    }

                    _tracker.ExpireUnsafe(now);
                    bool interruptCleared = _tracker.TryClearInterruptUnsafe(now);
                    return _tracker._attackers.Count > 0 || !interruptCleared ? CHECK_INTERVAL : 0;
                }
            }
        }
    }
}