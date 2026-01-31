using System;

namespace DOL.GS.Geometry;

public class Motion
{
    public static Motion Create(Position start, Coordinate destination, short withSpeed)
        => new Motion() { Start = start, Destination = destination, Speed = withSpeed };

    public int StartTimeInMilliSeconds { get; } = Environment.TickCount;
    public Position Start { get; init; } = Position.Nowhere;
    public Coordinate Destination { get; init; } = Coordinate.Nowhere;
    public short Speed { get; init; } = 0;

    public Position CurrentPosition
        => GetPositionAfter(Environment.TickCount - StartTimeInMilliSeconds);
    public double FullDistance => Destination.DistanceTo(Start, ignoreZ: true);
    public double RemainingDistance => Destination.DistanceTo(CurrentPosition, ignoreZ: true);
    public int FullDuration => FullDistance == 0 || Speed == 0 ? 0 : (int)(FullDistance / Speed * 1000);
    public int RemainingDuration => RemainingDistance == 0 || Speed == 0 ? 0 : (int)(RemainingDistance / Speed * 1000);

    public Position GetPositionAfter(int elapsedTimeInMilliSeconds)
    {
        if (Speed == 0 || Start.Coordinate == Destination) return Start;

        var distanceTravelled = Speed * elapsedTimeInMilliSeconds * 0.001;
        if (Destination == Coordinate.Nowhere) return Start + Vector.Create(Start.Orientation, distanceTravelled);

        var movementVector = Destination - Start.Coordinate;
        if (distanceTravelled > FullDistance) return Position.Create(Start.RegionID, Destination, movementVector.Orientation);

        return Start.With(movementVector.Orientation) + movementVector * (distanceTravelled / FullDistance);
    }
}