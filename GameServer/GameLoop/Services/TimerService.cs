using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;

namespace DOL.GS
{
    public sealed class TimerService : GameServiceBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private ServiceObjectView<ECSGameTimer> _view;

        public static TimerService Instance { get; } = new();

        public override void Tick()
        {
            ProcessPostedActionsParallel();

            try
            {
                _view = ServiceObjectStore.UpdateAndGetView<ECSGameTimer>(ServiceObjectType.Timer);
            }
            catch (Exception e)
            {
                log.Error("UpdateAndGetView failed", e);
                return;
            }

            if (ServerProperties.Properties.GAME_LOOP_PARALLEL_TIMERS)
            {
                _view.ExecuteForEach(TickInternal);
                return;
            }

            if (_view.IsSharded)
            {
                List<ECSGameTimer>[] shards = _view.Shards;
                int[] starts = _view.ShardStartIndices;
                int total = _view.TotalValidCount;

                for (int i = 0; i < shards.Length; i++)
                {
                    int count = (i < shards.Length - 1 ? starts[i + 1] : total) - starts[i];
                    List<ECSGameTimer> shard = shards[i];

                    for (int j = 0; j < count; j++)
                        TickInternal(shard[j]);
                }
            }
            else
            {
                for (int i = 0; i < _view.TotalValidCount; i++)
                    TickInternal(_view.Items[i]);
            }
        }

        private static void TickInternal(ECSGameTimer timer)
        {
            try
            {
                if (!GameServiceUtils.ShouldTick(timer.NextTick))
                    return;

                TickMonitor monitor = new();
                timer.Tick();

                if (monitor.IsLongTick(out long elapsedMs) && log.IsWarnEnabled)
                    log.Warn($"Long TimerService tick ({elapsedMs}ms) Callback: {timer.CallbackInfo?.DeclaringType}.{timer.CallbackInfo?.Name} Owner: {timer.Owner?.Name}");
            }
            catch (Exception e)
            {
                GameServiceUtils.HandleServiceException(e, nameof(TimerService), timer, timer.Owner);
            }
        }
    }

    public class ECSGameTimer : IShardedServiceObject
    {
        public delegate int ECSTimerCallback(ECSGameTimer timer);

        // Timers with intervals above this go to sleep on the timing wheel between ticks.
        private const int SLEEP_THRESHOLD_MS = 1000;

        private PropertyCollection _properties;
        private readonly Lock _propertiesLock = new();

        public GameObject Owner { get; }
        public ECSTimerCallback Callback { protected get; set; }
        public int Interval { get; set; }
        public long NextTick { get; protected set; }
        public bool IsAlive { get; private set; }

        public ShardedServiceObjectId ServiceObjectId { get; } = new(ServiceObjectType.Timer);
        ServiceObjectId IServiceObject.ServiceObjectId => ServiceObjectId;
        SchedulableServiceObjectId ISchedulableServiceObject.ServiceObjectId => ServiceObjectId;

        public int TimeUntilElapsed => (int)(NextTick - GameLoop.GameLoopTime);
        public MethodInfo CallbackInfo => Callback?.GetMethodInfo();

        public ECSGameTimer(GameObject timerOwner)
        {
            Owner = timerOwner;
        }

        public ECSGameTimer(GameObject timerOwner, ECSTimerCallback callback)
        {
            Owner = timerOwner;
            Callback = callback;
        }

        public ECSGameTimer(GameObject timerOwner, ECSTimerCallback callback, int interval)
        {
            Owner = timerOwner;
            Callback = callback;
            Interval = interval;
            Start(interval);
        }

        public void Start()
        {
            Start(Interval <= 0 ? 500 : Interval);
        }

        public void Start(int interval)
        {
            Interval = interval;
            NextTick = GameLoop.GameLoopTime + interval;

            if (ServiceObjectStore.Schedule(this, NextTick))
                IsAlive = true;
        }

        public void Stop()
        {
            if (ServiceObjectStore.Remove(this))
                IsAlive = false;
        }

        public void Tick()
        {
            if (Callback != null)
                Interval = Callback.Invoke(this);

            if (Interval <= 0)
            {
                Stop();
                return;
            }

            NextTick += Interval;

            if (Interval >= SLEEP_THRESHOLD_MS)
                ServiceObjectStore.Schedule(this, NextTick);
        }

        public PropertyCollection Properties
        {
            get
            {
                if (_properties == null)
                {
                    lock (_propertiesLock)
                    {
                        _properties ??= new PropertyCollection();
                    }
                }

                return _properties;
            }
        }

        public override string ToString()
        {
            return $"callback:{CallbackInfo?.DeclaringType}.{CallbackInfo?.Name}";
        }
    }

    public abstract class ECSGameTimerWrapperBase : ECSGameTimer
    {
        protected ECSGameTimerWrapperBase(GameObject owner) : base(owner)
        {
            Callback = new ECSTimerCallback(OnTick);
        }

        protected abstract int OnTick(ECSGameTimer timer);
    }
}