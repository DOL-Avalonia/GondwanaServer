using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;

namespace DOL.GS
{
    public static class GameLoop
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        public const string THREAD_NAME = "GameLoop";
        private static Thread _gameLoopThread;
        private static GameLoopThreadPool _threadPool;
        private static GameLoopTickPacer _tickPacer;
        private static bool _running;
        private static List<TickState> _tickSequence;
        public static int DegreeOfParallelism { get; } = Math.Max(1, Environment.ProcessorCount);
        public static double TickDuration { get; private set; } = 50.0;
        public static long GameLoopTime { get; private set; }
        public static bool IsRunning => Volatile.Read(ref _running);
        public static IGameService ActiveService { get; private set; }
        public static bool Init()
        {
            if (Interlocked.CompareExchange(ref _running, true, false))
                return false;
            int tickRate = ServerProperties.Properties.GAME_LOOP_TICK_RATE;
            if (tickRate < 1)
                tickRate = 20;
            TickDuration = 1000.0 / tickRate;
            _gameLoopThread = new Thread(Run) { Name = THREAD_NAME, IsBackground = true };
            _gameLoopThread.Start();
            _tickPacer = new GameLoopTickPacer(TickDuration);
            _tickPacer.Start();
            return true;
        }
        public static void Exit()
        {
            if (!Interlocked.CompareExchange(ref _running, false, true))
                return;
            if (_gameLoopThread != null && Thread.CurrentThread != _gameLoopThread && _gameLoopThread.IsAlive)
                _gameLoopThread.Join();
            _tickPacer?.Stop();
            _threadPool?.Dispose();
        }
        public static List<(int, double)> GetAverageTps()
        {
            return _tickPacer?.Stats?.GetAverageTicks() ?? new List<(int, double)>();
        }
        public static void ExecuteForEach<T>(List<T> items, int toExclusive, Action<T> action)
        {
            _threadPool.ExecuteForEach(items, toExclusive, action);
        }
        public static void ExecuteForEachSharded<T>(List<T>[] shards, int[] shardStartIndices, int totalCount, Action<T> action)
        {
            _threadPool.ExecuteForEachSharded(shards, shardStartIndices, totalCount, action);
        }
        public static List<T> GetListForTick<T>() where T : IPooledList<T>
        {
            return _threadPool != null ? _threadPool.GetListForTick<T>() : new();
        }
        public static T GetObjectForTick<T>() where T : IPooledObject<T>, new()
        {
            return _threadPool != null ? _threadPool.GetObjectForTick<T>() : new();
        }

        private static void Run()
        {
            _threadPool = DegreeOfParallelism == 1
                ? new GameLoopThreadPoolSingleThreaded()
                : new GameLoopThreadPoolMultiThreaded(Math.Min(DegreeOfParallelism, 128));
            _threadPool.Init();
            BuildTickSequence();
            while (Volatile.Read(ref _running))
            {
                try
                {
                    TickServices();
                    GameLoopTime = _tickPacer.WaitForNextTick();
                }
                catch (Exception e)
                {
                    log.Fatal("Critical error encountered in GameLoop", e);
                    GameServer.Instance.Stop();
                    break;
                }
            }
            log.Info($"Thread \"{Thread.CurrentThread.Name}\" is stopping");
        }
        private static void TickServices()
        {
            for (int i = 0; i < _tickSequence.Count; i++)
            {
                TickState tickState = _tickSequence[i];
                SynchronizationContext prevCtx = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(tickState.ServiceContext);
                ActiveService = tickState.ServiceContext.TargetService;
                try
                {
                    tickState.TickAction();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(prevCtx);
                    ActiveService = null;
                }
            }
        }
        private static void BuildTickSequence()
        {
            _tickSequence = new();
            AddStep(GameLoopService.Instance, GameLoopService.Instance.Tick);
            AddStep(TimerService.Instance, TimerService.Instance.Tick);
            AddStep(ZoneService.Instance, ZoneService.Instance.Tick);
            AddStep(RolloverSchedulerService.Instance, RolloverSchedulerService.Instance.Tick);
            AddStep(ClientService.Instance, ClientService.Instance.Tick);
            AddStep(RelocationService.Instance, RelocationService.Instance.Tick);
            // Phase 2+: ClientService, ZoneService...
            // Phase 3+: NpcService, AttackService, CastingService, EffectService, MovementService, ReaperService, RolloverSchedulerService...
            static void AddStep(IGameService service, Action action)
            {
                _tickSequence.Add(new TickState(action, GameServiceContext.GetContextFor(service)));
            }

            HouseRentService.Initialize();
        }
        private sealed class TickState
        {
            public readonly Action TickAction;
            public readonly GameServiceSynchronizationContext ServiceContext;
            public TickState(Action tickAction, GameServiceSynchronizationContext serviceContext)
            {
                TickAction = tickAction;
                ServiceContext = serviceContext;
            }
        }
    }
}