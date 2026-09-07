namespace DOL.GS
{
    public enum ServiceObjectType
    {
        Client,
        Brain,
        AttackComponent,
        CastingComponent,
        Effect,
        EffectListComponent,
        MovementComponent,
        CraftComponent,
        SubZoneObject,
        LivingBeingKilled,
        Timer
    }

    public interface IServiceObject
    {
        ServiceObjectId ServiceObjectId { get; }
    }

    public interface ISchedulableServiceObject : IServiceObject
    {
        new SchedulableServiceObjectId ServiceObjectId { get; }
    }

    public interface IShardedServiceObject : ISchedulableServiceObject
    {
        new ShardedServiceObjectId ServiceObjectId { get; }
    }

    public interface IServiceObjectArray { }
}