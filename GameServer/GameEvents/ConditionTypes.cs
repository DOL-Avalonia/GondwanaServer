namespace DOL.GameEvents
{
    public enum StartingConditionType
    {
        Timer,
        Kill,
        Event,
        Money,
        Interval,
        Areaxevent,
        Quest,
        Switch,
        Onetime
    }

    public enum EndingConditionType
    {
        Timer,
        Kill,
        StartingEvent,
        AreaEvent,
        TextNPC,
        Switch,
        Family
    }
    
    public enum InstancedConditionTypes
    {
        All,
        Player,
        Group,
        Guild,
        Battlegroup,
        GroupOrSolo,
        GuildOrSolo,
    }

    public enum FamilyConditionType
    {
        EndAction = 0, // Event must be registered through EndingAction.StartEvent
        EventStarted, // Event must be started, can end
        EventRunning, // Event must be currently running
        EventEnded, // Event must have ended, regardless of ending condition
    }

    public enum FamilyOrdering
    {
        Unordered = 0, // Child events can be activated in any order
        Soft, // Child events need a specific order, but don't do anything on wrong order
        Hidden, // Child events need a specific order, only reset the saved order on wrong order, don't prevent starting
        Strict, // Child events need a specific order, prevent starting with wrong order
        Stop, // Child events need a specific order, and stop all family events on error
        Reset // Child events need a specific order, and reset all family events on error
    }
}