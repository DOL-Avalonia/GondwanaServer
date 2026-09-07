namespace DOL.GS
{
    public sealed class GameLoopService : GameServiceBase
    {
        public static GameLoopService Instance { get; } = new();
        public override void Tick() => ProcessPostedActionsParallel();
    }
}