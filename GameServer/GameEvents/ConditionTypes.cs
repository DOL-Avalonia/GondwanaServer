﻿namespace DOL.GameEvents
{
    public enum StartingConditionType
    {
        Timer,
        Kill,
        Event,
        Money,
        Interval,
        Areaxevent,
        Quest
    }

    public enum EndingConditionType
    {
        Timer,
        Kill,
        StartingEvent,
        AreaEvent,
        TextNPC
    }
    public enum InstancedConditionTypes
    {
        All,
        Player,
        Group,
        Guild,
        Battlegroup
    }
}