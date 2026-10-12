using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DOL.GS.PacketHandler;
using log4net;

namespace DOL.GS
{
    /// Bridges async work (translations, DB) and the game loop.
    ///  - Message(...): send a possibly-still-translating text. If the text is already ready (same language / cached): sent immediately, no thread hop.
    ///  - Then(...):    run a callback with the task's result ON THE GAME LOOP (inline if the result is ready and we are already on the loop).
    ///  - Fire(...):    fire-and-forget async body that ONLY sends packets; exceptions are logged instead of silently lost.
    public static class GameLoopAsync
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public static bool IsOnGameLoop => SynchronizationContext.Current is GameServiceSynchronizationContext;

        public static void Message(GamePlayer player, Task<string> text, eChatType type, eChatLoc loc)
        {
            if (player == null || text == null) return;
            if (text.IsCompletedSuccessfully) { if (!string.IsNullOrEmpty(text.Result)) player.Out.SendMessage(text.Result, type, loc); return; }
            Then(text, (player, type, loc), static (str, s) =>
            {
                if (s.player.ObjectState == GameObject.eObjectState.Active && !string.IsNullOrEmpty(str)) s.player.Out.SendMessage(str, s.type, s.loc);
            });
        }

        public static void Then<T>(Task<T> task, Action<T> onDone) => Then(task, onDone, static (r, a) => a(r));

        public static void Then<T, TState>(Task<T> task, TState state, Action<T, TState> onDone)
        {
            if (task.IsCompletedSuccessfully) { Deliver(task.Result, state, onDone); return; }
            task.ContinueWith(static (t, boxed) =>
            {
                var (state, onDone) = ((TState, Action<T, TState>))boxed!;
                if (t.IsFaulted) { log.Error("GameLoopAsync.Then: async task failed", t.Exception); return; }
                if (t.IsCanceled) return;
                Deliver(t.Result, state, onDone);
            }, (state, onDone), TaskContinuationOptions.ExecuteSynchronously);
        }

        private static void Deliver<T, TState>(T result, TState state, Action<T, TState> onDone)
        {
            if (IsOnGameLoop) { onDone(result, state); return; }
            ClientService.Instance.Post(static s => s.onDone(s.result, s.state), (result, state, onDone));
        }

        public static void Fire(Func<Task> work, string context = null)
        {
            Task t;
            try { t = work(); }
            catch (Exception e) { log.Error($"GameLoopAsync.Fire ({context}) threw synchronously", e); return; }
            if (t == null) return;
            if (t.IsCompleted) { if (t.IsFaulted) log.Error($"GameLoopAsync.Fire ({context}) failed", t.Exception); return; }
            t.ContinueWith(static (done, ctx) => log.Error($"GameLoopAsync.Fire ({ctx}) failed", done.Exception),
                           context, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}