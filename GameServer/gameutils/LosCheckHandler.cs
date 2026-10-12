using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace DOL.GS
{
    /// <summary>
    /// Answer to a line-of-sight request.
    /// </summary>
    public enum LosCheckResponse : byte
    {
        None = 0,      // internal: not answered yet
        True = 1,      // the target is visible from the source
        False = 2,     // blocked
        Timeout = 3    // the client never answered within LOS_CHECK_TIMEOUT
    }

    /// <summary>
    /// Anything that wants the answer of a LoS request implements this (brains, spell handlers, siege weapons...).
    /// </summary>
    public interface ILosCheckListener
    {
        void HandleLosCheckResponse(GamePlayer losChecker, LosCheckResponse response, ushort sourceId, ushort targetId);
    }

    public static class LosCheckResponseExtensions
    {
        public static bool IsVisible(this LosCheckResponse response, bool assumeVisibleOnTimeout = false)
            => response == LosCheckResponse.True || (assumeVisibleOnTimeout && response == LosCheckResponse.Timeout);

        /// <summary>
        /// Old DOL convention: bit 0x100 set = visible.
        /// </summary>
        public static ushort ToLegacyCode(this LosCheckResponse response, bool assumeVisibleOnTimeout = true)
            => response.IsVisible(assumeVisibleOnTimeout) ? (ushort)0x100 : (ushort)0x000;
    }

    /// <summary>
    /// Bridges the old <see cref="CheckLOSResponse"/> delegate to the new listener API.
    /// Old scripts keep working, but now deduplicated and cached.
    /// </summary>
    public sealed class LegacyLosCheckListener : ILosCheckListener
    {
        private readonly CheckLOSResponse _callback;
        private readonly bool _assumeVisibleOnTimeout;

        public LegacyLosCheckListener(CheckLOSResponse callback, bool assumeVisibleOnTimeout = true)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _assumeVisibleOnTimeout = assumeVisibleOnTimeout;
        }

        public void HandleLosCheckResponse(GamePlayer losChecker, LosCheckResponse response, ushort sourceId, ushort targetId)
            => _callback(losChecker, response.ToLegacyCode(_assumeVisibleOnTimeout), targetId);
    }

    /// <summary>
    /// Bridges the old <see cref="CheckLOSMgrResponse"/>
    /// delegate (source + target ids) to the new listener API.
    /// </summary>
    public sealed class LegacyLosCheckMgrListener : ILosCheckListener
    {
        private readonly CheckLOSMgrResponse _callback;
        private readonly bool _assumeVisibleOnTimeout;

        public LegacyLosCheckMgrListener(CheckLOSMgrResponse callback, bool assumeVisibleOnTimeout = true)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _assumeVisibleOnTimeout = assumeVisibleOnTimeout;
        }

        public void HandleLosCheckResponse(GamePlayer losChecker, LosCheckResponse response, ushort sourceId, ushort targetId)
            => _callback(losChecker, response.ToLegacyCode(_assumeVisibleOnTimeout), sourceId, targetId);
    }

    /// <summary>
    /// One instance per player. The player is the "checker": its client computes the line of sight between two objects.
    /// - Several callers asking the same (source, target) share one packet.
    /// - Answers are cached while neither object moved (GameLiving.LastMovementTick).
    /// - When Gondwana's LosCheckMgr has mesh data for the zone the answer is computed on the server: no packet at all.
    /// - A request without answer ends with LosCheckResponse.Timeout, nothing can hang forever.
    /// </summary>
    public sealed class LosCheckHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private const int MAX_POOLED_TIMERS = 32;
        private static long s_requestsSent, s_responses, s_lateResponses, s_timeouts, s_deduplicated, s_cacheHits, s_geometryResolved;

        private readonly GamePlayer _owner;
        private readonly Dictionary<LosCheckKey, LosCheckTimer> _timers = new();
        private readonly Queue<LosCheckTimer> _timerPool = new();
        private readonly Lock _lock = new();

        public LosCheckHandler(GamePlayer owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public GamePlayer Owner => _owner;

        /// <summary>Pending + cached entries of this player.</summary>
        public int ActiveCheckCount { get { lock (_lock) return _timers.Count; } }

        public static int Timeout => Math.Max(200, Properties.LOS_CHECK_TIMEOUT);
        public static int CacheLifetime => Math.Max(0, Properties.LOS_CHECK_CACHE_LIFETIME);

        #region Choosing a checker

        public static bool CanReplyToLosChecks(GamePlayer player)
            => player != null
               && player.ObjectState == GameObject.eObjectState.Active
               && player.Client != null
               && player.Client.ClientState == GameClient.eClientState.Playing;

        /// <summary>
        /// Picks a player able to answer a LoS request between source and target:
        /// source player, source's owner, target player, target's owner, then a random player near the source.
        /// Returns null when nobody can answer (callers should then proceed without LoS).
        /// </summary>
        public static GamePlayer FindLosChecker(GameObject source, GameObject target)
        {
            if (source == null || target == null) return null;

            if (source is GamePlayer sourcePlayer && CanReplyToLosChecks(sourcePlayer)) return sourcePlayer;
            if (source is GameLiving sourceLiving && sourceLiving.GetPlayerOwner() is GamePlayer sourceOwner && CanReplyToLosChecks(sourceOwner)) return sourceOwner;
            if (target is GamePlayer targetPlayer && CanReplyToLosChecks(targetPlayer)) return targetPlayer;
            if (target is GameLiving targetLiving && targetLiving.GetPlayerOwner() is GamePlayer targetOwner && CanReplyToLosChecks(targetOwner)) return targetOwner;

            return FindRandomLosChecker(source);
        }

        /// <summary>
        /// Uniformly random playing player within visibility distance (reservoir sampling: no list allocation).
        /// </summary>
        public static GamePlayer FindRandomLosChecker(GameObject around)
        {
            if (around == null || around.CurrentRegion == null) return null;

            GamePlayer chosen = null;
            int seen = 0;

            foreach (GamePlayer player in around.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (!CanReplyToLosChecks(player)) continue;
                seen++;
                if (Util.Random(seen - 1) == 0) chosen = player;
            }

            return chosen;
        }

        #endregion

        #region Requests

        /// <summary>
        /// Asks whether <paramref name="source"/> can see <paramref name="target"/>.
        /// Returns false when the request cannot be made (invalid objects, different regions, this player is not playing);
        /// the listener is then NOT called. Returns true when the listener will be (or already was) called.
        /// The listener may be invoked synchronously (cache / server geometry): never hold your own lock while calling this.
        /// </summary>
        public bool StartLosCheck(GameObject source, GameObject target, ILosCheckListener listener)
        {
            if (source == null || target == null || listener == null) return false;
            if (!CanReplyToLosChecks(_owner)) return false;

            Region region = source.CurrentRegion;
            if (region == null || target.CurrentRegion != region) return false;

            ushort sourceId = (ushort)source.ObjectID;
            ushort targetId = (ushort)target.ObjectID;
            if (sourceId == 0 || targetId == 0) return false;

            if (sourceId == targetId)
            {
                InvokeListener(listener, LosCheckResponse.True, sourceId, targetId);
                return true;
            }

            long sourceMovementTick = (source as GameLiving)?.LastMovementTick ?? 0;
            long targetMovementTick = (target as GameLiving)?.LastMovementTick ?? 0;

            LosCheckTimer timer;
            LosCheckResponse cachedResponse = LosCheckResponse.None;
            bool needsFreshCheck = false;

            lock (_lock)
            {
                LosCheckKey key = new(sourceId, targetId);

                if (_timers.TryGetValue(key, out timer))
                {
                    if (!timer.HasCachedResponse)
                    {
                        timer.AddListener(listener);
                        Interlocked.Increment(ref s_deduplicated);
                        return true;
                    }

                    if (timer.IsCacheValid
                        && sourceMovementTick <= timer.SourceLastMovementTick
                        && targetMovementTick <= timer.TargetLastMovementTick)
                    {
                        cachedResponse = timer.Response;
                        timer.RefreshCacheExpiry();
                    }
                    else
                    {
                        timer.Setup(sourceId, targetId, sourceMovementTick, targetMovementTick, listener);
                        needsFreshCheck = true;
                    }
                }
                else
                {
                    timer = RentTimer();
                    timer.Setup(sourceId, targetId, sourceMovementTick, targetMovementTick, listener);
                    _timers[key] = timer;
                    needsFreshCheck = true;
                }
            }

            if (!needsFreshCheck)
            {
                Interlocked.Increment(ref s_cacheHits);
                InvokeListener(listener, cachedResponse, sourceId, targetId);
                return true;
            }

            // Gondwana bonus: server side geometry, no round-trip to the client.
            if (TryResolveWithServerGeometry(source, target, out LosCheckResponse geometryResponse))
            {
                Interlocked.Increment(ref s_geometryResolved);
                timer.OnResponseReceived(sourceId, targetId, geometryResponse);
                return true;
            }

            SendRequestPacket(sourceId, targetId);
            return true;
        }

        /// <summary>
        /// Called by CheckLOSResponseHandler (packet 0xD0).
        /// Safe to call from any thread.
        /// </summary>
        public void HandleLosResponse(ushort sourceId, ushort targetId, LosCheckResponse response)
        {
            Interlocked.Increment(ref s_responses);

            if (SynchronizationContext.Current is GameServiceSynchronizationContext)
                ProcessResponse(sourceId, targetId, response);
            else
                TimerService.Instance.Post(
                    static state => state.handler.ProcessResponse(state.sourceId, state.targetId, state.response),
                    (handler: this, sourceId, targetId, response));
        }

        private void ProcessResponse(ushort sourceId, ushort targetId, LosCheckResponse response)
        {
            LosCheckTimer timer;

            lock (_lock)
            {
                if (!_timers.TryGetValue(new(sourceId, targetId), out timer) || timer.Response != LosCheckResponse.None)
                {
                    Interlocked.Increment(ref s_lateResponses);
                    return;
                }
            }

            timer.OnResponseReceived(sourceId, targetId, response);
        }

        /// <summary>
        /// Drops every pending/cached entry (player quits, link death).
        /// Pending listeners get a Timeout so nothing hangs.
        /// </summary>
        public void Reset()
        {
            List<(ILosCheckListener[] listeners, ushort sourceId, ushort targetId)> pending = null;

            lock (_lock)
            {
                foreach (LosCheckTimer timer in _timers.Values)
                {
                    if (timer.Response == LosCheckResponse.None && timer.ListenerCount > 0)
                    {
                        pending ??= new();
                        pending.Add((timer.DrainListeners(), timer.SourceObjectId, timer.TargetObjectId));
                    }
                    timer.Clear();
                }

                _timers.Clear();
                _timerPool.Clear();
            }

            if (pending == null) return;

            foreach (var entry in pending)
                InvokeListeners(entry.listeners, LosCheckResponse.Timeout, entry.sourceId, entry.targetId);
        }

        #endregion

        #region Internals

        private static bool TryResolveWithServerGeometry(GameObject source, GameObject target, out LosCheckResponse response)
        {
            response = LosCheckResponse.None;

            if (!Properties.LOS_CHECK_USE_SERVER_GEOMETRY || !Properties.LOS_MGR_ENABLE) return false;

            Zone sourceZone = source.CurrentZone;
            Zone targetZone = target.CurrentZone;
            if (sourceZone == null || targetZone == null) return false;
            if (!LosCheckMgr.HasDataFor(sourceZone) || !LosCheckMgr.HasDataFor(targetZone)) return false;

            RaycastStats stats = new RaycastStats();
            float collisionDistance = LosCheckMgr.GetCollisionDistance(source, target, ref stats);
            response = float.IsPositiveInfinity(collisionDistance) ? LosCheckResponse.True : LosCheckResponse.False;
            return true;
        }

        private void SendRequestPacket(ushort sourceId, ushort targetId)
        {
            IPacketLib packetLib = _owner.Out;
            if (packetLib == null) return;

            using (GSTCPPacketOut pak = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(packetLib.GetPacketCode(eServerPackets.CheckLOSRequest)))
            {
                pak.WriteShort(sourceId);
                pak.WriteShort(targetId);
                pak.WriteShort(0x00);
                pak.WriteShort(0x00);
                packetLib.SendTCP(pak);
            }

            Interlocked.Increment(ref s_requestsSent);
        }

        private LosCheckTimer RentTimer()
            => _timerPool.TryDequeue(out LosCheckTimer timer) ? timer : new LosCheckTimer(this);

        /// <summary>
        /// Must be called with _lock held.
        /// </summary>
        private void ReturnTimerToPoolLocked(LosCheckTimer timer)
        {
            LosCheckKey key = new(timer.SourceObjectId, timer.TargetObjectId);
            if (_timers.TryGetValue(key, out LosCheckTimer current) && current == timer)
                _timers.Remove(key);

            timer.Clear();

            if (_timerPool.Count < MAX_POOLED_TIMERS)
                _timerPool.Enqueue(timer);
        }

        private void InvokeListeners(ILosCheckListener[] listeners, LosCheckResponse response, ushort sourceId, ushort targetId)
        {
            for (int i = 0; i < listeners.Length; i++)
                InvokeListener(listeners[i], response, sourceId, targetId);
        }

        private void InvokeListener(ILosCheckListener listener, LosCheckResponse response, ushort sourceId, ushort targetId)
        {
            try
            {
                listener.HandleLosCheckResponse(_owner, response, sourceId, targetId);
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error($"Error in LoS listener {listener.GetType().Name} (source {sourceId}, target {targetId}, response {response}, checker {_owner.Name})", e);
            }
        }

        #endregion

        #region Statistics

        public static List<string> GetStatisticsReport(string lang)
        {
            long sent = Interlocked.Read(ref s_requestsSent);
            long responses = Interlocked.Read(ref s_responses);
            long late = Interlocked.Read(ref s_lateResponses);
            long timeouts = Interlocked.Read(ref s_timeouts);
            long dedup = Interlocked.Read(ref s_deduplicated);
            long cache = Interlocked.Read(ref s_cacheHits);
            long geometry = Interlocked.Read(ref s_geometryResolved);
            long saved = dedup + cache + geometry;
            double savedPercent = sent + saved == 0 ? 0 : 100.0 * saved / (sent + saved);

            return new List<string>
            {
                LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Stats.Packets", sent, responses, late, timeouts),
                LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Stats.Saved", dedup, cache, geometry, saved, savedPercent)
            };
        }

        public static void ResetStatistics()
        {
            Interlocked.Exchange(ref s_requestsSent, 0);
            Interlocked.Exchange(ref s_responses, 0);
            Interlocked.Exchange(ref s_lateResponses, 0);
            Interlocked.Exchange(ref s_timeouts, 0);
            Interlocked.Exchange(ref s_deduplicated, 0);
            Interlocked.Exchange(ref s_cacheHits, 0);
            Interlocked.Exchange(ref s_geometryResolved, 0);
        }

        #endregion

        #region Nested types

        private readonly record struct LosCheckKey(ushort SourceObjectId, ushort TargetObjectId);

        /// <summary>
        /// One (source, target) pair. Runs as an ECSGameTimer on the GameLoop:
        /// first tick = timeout if no answer came, later ticks = cache expiry.
        /// All state changes happen under the handler lock; listeners are always invoked outside of it.
        /// </summary>
        private sealed class LosCheckTimer : ECSGameTimerWrapperBase
        {
            private readonly LosCheckHandler _handler;
            private readonly List<ILosCheckListener> _listeners = new(4);
            private long _responseExpireTime;

            public ushort SourceObjectId { get; private set; }
            public ushort TargetObjectId { get; private set; }
            public long SourceLastMovementTick { get; private set; }
            public long TargetLastMovementTick { get; private set; }
            public LosCheckResponse Response { get; private set; }
            public bool IsPooled { get; private set; } = true;
            public int ListenerCount => _listeners.Count;
            public bool HasCachedResponse => _responseExpireTime > 0;
            public bool IsCacheValid => HasCachedResponse && !GameServiceUtils.ShouldTick(_responseExpireTime);

            public LosCheckTimer(LosCheckHandler handler) : base(handler._owner)
            {
                _handler = handler;
            }

            /// <summary>
            /// Lock held by caller.
            /// </summary>
            public void Setup(ushort sourceId, ushort targetId, long sourceMovementTick, long targetMovementTick, ILosCheckListener listener)
            {
                IsPooled = false;
                SourceObjectId = sourceId;
                TargetObjectId = targetId;
                SourceLastMovementTick = sourceMovementTick;
                TargetLastMovementTick = targetMovementTick;
                Response = LosCheckResponse.None;
                _responseExpireTime = 0;
                _listeners.Clear();
                _listeners.Add(listener);
                Start(LosCheckHandler.Timeout);
            }

            public void AddListener(ILosCheckListener listener) => _listeners.Add(listener);

            public void RefreshCacheExpiry() => _responseExpireTime = GameLoop.GameLoopTime + LosCheckHandler.CacheLifetime;

            public ILosCheckListener[] DrainListeners()
            {
                if (_listeners.Count == 0) return Array.Empty<ILosCheckListener>();
                ILosCheckListener[] listeners = _listeners.ToArray();
                _listeners.Clear();
                return listeners;
            }

            /// <summary>
            /// Lock held by caller.
            /// </summary>
            public void Clear()
            {
                IsPooled = true;
                SourceObjectId = 0;
                TargetObjectId = 0;
                SourceLastMovementTick = 0;
                TargetLastMovementTick = 0;
                Response = LosCheckResponse.None;
                _responseExpireTime = 0;
                _listeners.Clear();
                Stop();
            }

            /// <summary>
            /// Client (or server geometry) answered. Lock NOT held by caller.
            /// </summary>
            internal void OnResponseReceived(ushort sourceId, ushort targetId, LosCheckResponse response)
            {
                ILosCheckListener[] listeners;

                lock (_handler._lock)
                {
                    if (IsPooled || SourceObjectId != sourceId || TargetObjectId != targetId || Response != LosCheckResponse.None)
                    {
                        Interlocked.Increment(ref s_lateResponses);
                        return;
                    }

                    Response = response;
                    listeners = DrainListeners();

                    if (LosCheckHandler.CacheLifetime > 0)
                    {
                        RefreshCacheExpiry();
                        Start(LosCheckHandler.CacheLifetime);
                    }
                    else
                    {
                        _handler.ReturnTimerToPoolLocked(this);
                    }
                }

                _handler.InvokeListeners(listeners, response, sourceId, targetId);
            }

            protected override int OnTick(ECSGameTimer timer)
            {
                ILosCheckListener[] listeners;
                ushort sourceId, targetId;

                lock (_handler._lock)
                {
                    if (IsPooled) return 0;

                    if (Response != LosCheckResponse.None)
                    {
                        if (!GameServiceUtils.ShouldTick(_responseExpireTime))
                            return (int)Math.Max(1, _responseExpireTime - GameLoop.GameLoopTime);

                        _handler.ReturnTimerToPoolLocked(this);
                        return 0;
                    }

                    Interlocked.Increment(ref s_timeouts);
                    Response = LosCheckResponse.Timeout;
                    sourceId = SourceObjectId;
                    targetId = TargetObjectId;
                    listeners = DrainListeners();
                    _handler.ReturnTimerToPoolLocked(this);
                }

                _handler.InvokeListeners(listeners, LosCheckResponse.Timeout, sourceId, targetId);
                return 0;
            }
        }
        #endregion
    }
}