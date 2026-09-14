/*
namespace DOL.GS
{
    /// <summary>
    /// Marshals work onto the game loop so packets/pooled objects
    /// are built on a loop thread, in deterministic order.
    /// </summary>
    public static class GameLoopPost
    {
        public static void Run(System.Action action)
        {
            if (GameLoop.ActiveService != null) { action(); return; }
            GameLoopService.Instance.Post(static a => a(), action);
        }

        public static void Run<TState>(System.Action<TState> action, TState state)
        {
            if (GameLoop.ActiveService != null) { action(state); return; }
            GameLoopService.Instance.Post(action, state);
        }
    }
}*/