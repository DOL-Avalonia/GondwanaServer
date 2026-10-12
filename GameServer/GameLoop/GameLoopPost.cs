using DOL.GS.PacketHandler;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DOL.GS
{
    public static class GameLoopPost
    {
        public static bool IsOnGameLoop => SynchronizationContext.Current is GameServiceSynchronizationContext;

        public static void Run(Action action)
        {
            if (IsOnGameLoop) action();
            else GameLoopService.Instance.Post(static a => a(), action);
        }

        public static void Run<TState>(Action<TState> action, TState state)
        {
            if (IsOnGameLoop) action(state);
            else GameLoopService.Instance.Post(action, state);
        }

        /// <summary>
        /// Run on the game loop, ordered after everything already posted for this client.
        /// </summary>
        public static void RunForClient<TState>(GameClient client, Action<TState> action, TState state)
        {
            if (IsOnGameLoop) { action(state); return; }
            if (client != null) client.Post(action, state);
            else GameLoopService.Instance.Post(action, state);
        }
    }
}