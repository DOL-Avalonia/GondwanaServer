using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;

namespace DOL.GS
{
    /// Ticks script modules (PvP/RvR/Arena) sequentially on the game-loop thread, after ZoneService
    /// (spatial index populated) and before ClientService (packets they queue are flushed in the same tick).
    public sealed class ModuleService : GameServiceBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        public static ModuleService Instance { get; } = new();

        private readonly Lock _lock = new();
        private readonly List<IGameModule> _modules = new();
        private IGameModule[] _snapshot = Array.Empty<IGameModule>();

        public static bool IsCurrentContext =>
            SynchronizationContext.Current is GameServiceSynchronizationContext ctx && ReferenceEquals(ctx.TargetService, Instance);

        /// <summary>Fire-and-forget on the module thread (inline if already there, otherwise posted).</summary>
        public static void Run(Action action)
        {
            if (IsCurrentContext) action();
            else Instance.Post(static a => a(), action);
        }

        /// <summary>
        /// Synchronous execution. From a non-game-loop thread it blocks until the next ModuleService tick.
        /// From another game-loop service (timer/casting/client worker) it cannot be deferred, so it runs inline
        /// serialized by <paramref name="gate"/> - the module's Tick() must take the same gate.
        /// </summary>
        private static bool CanRunInline => !GameLoop.IsRunning || SynchronizationContext.Current is GameServiceSynchronizationContext || Thread.CurrentThread.Name == GameLoop.THREAD_NAME;

        public static void Sync(Lock gate, Action action)
        {
            if (IsCurrentContext) { action(); return; }
            if (CanRunInline) { lock (gate) action(); return; }
            GameServiceContext.GetContextFor(Instance).Send(static a => ((Action)a)!(), action);
        }

        public static T Query<T>(Lock gate, Func<T> func)
        {
            if (IsCurrentContext) return func();
            if (CanRunInline) { lock (gate) return func(); }
            T result = default;
            GameServiceContext.GetContextFor(Instance).Send(_ => result = func(), null);
            return result;
        }

        public static void Run<TState>(Action<TState> action, TState state)
        {
            if (IsCurrentContext) action(state);
            else Instance.Post(action, state);
        }

        public static T Invoke<T>(Func<T> func)
        {
            if (IsCurrentContext) return func();
            T result = default;
            GameServiceContext.GetContextFor(Instance).Send(_ => result = func(), null);
            return result;
        }

        public void Register(IGameModule module)
        {
            lock (_lock) { if (_modules.Contains(module)) return; module.NextTick = 0; _modules.Add(module); _snapshot = _modules.ToArray(); }
        }

        public void Unregister(IGameModule module)
        {
            lock (_lock) { if (_modules.Remove(module)) _snapshot = _modules.ToArray(); }
        }

        public override void Tick()
        {
            ProcessPostedActions();
            IGameModule[] modules = _snapshot;
            long now = GameLoop.GameLoopTime;
            for (int i = 0; i < modules.Length; i++)
            {
                IGameModule module = modules[i];
                if (!GameServiceUtils.ShouldTick(module.NextTick)) continue;
                TickMonitor monitor = new();
                int interval;
                try { interval = module.Tick(); }
                catch (Exception e) { interval = 1000; log.Error($"[ModuleService] '{module.Name}' threw", e); }
                module.NextTick = now + Math.Max((int)GameLoop.TickDuration, interval);
                if (monitor.IsLongTick(out long ms) && log.IsWarnEnabled) log.Warn($"[ModuleService] Long tick ({ms}ms) in '{module.Name}'");
            }
        }
    }

    public interface IGameModule
    {
        string Name { get; }
        long NextTick { get; set; }
        int Tick();
    }
}