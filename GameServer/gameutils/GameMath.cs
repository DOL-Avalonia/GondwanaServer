using System;
using DOL.GS;
using DOL.GS.Geometry;
using System.Linq;
using System.Numerics;

namespace DOL
{
    public static class GameMath
    {
        public static Vector2 ToVector2(this Vector3 v) => new Vector2(v.X, v.Y);
        
        /// <summary>
        /// The factor to convert a heading value to radians
        /// </summary>
        /// <remarks>
        /// Heading to degrees = heading * (360 / 4096)
        /// Degrees to radians = degrees * (PI / 180)
        /// </remarks>
        public const float HEADING_TO_RADIAN = (360.0f / 4096.0f) * ((float)Math.PI / 180.0f);

        /// <summary>
        /// The factor to convert radians to a heading value
        /// </summary>
        /// <remarks>
        /// Radians to degrees = radian * (180 / PI)
        /// Degrees to heading = degrees * (4096 / 360)
        /// </remarks>
        public const float RADIAN_TO_HEADING = (180.0f / (float)Math.PI) * (4096.0f / 360.0f);

        // Coordinate calculation functions in DOL are standard trigonometric functions, but
        // with some adjustments to account for the different coordinate system that DOL uses
        // compared to the standard Cartesian coordinates used in trigonometry.
        //
        // Cartesian grid:
        //        90
        //         |
        // 180 --------- 0
        //         |
        //        270
        //        
        // DOL Heading grid:
        //       2048
        //         |
        // 1024 ------- 3072
        //         |
        //         0
        // 
        // The Cartesian grid is 0 at the right side of the X-axis and increases counter-clockwise.
        // The DOL Heading grid is 0 at the bottom of the Y-axis and increases clockwise.
        // General trigonometry and the System.Math library use the Cartesian grid.

        /// <summary>
        /// Get the heading to a point
        /// </summary>
        /// <param name="origin">Source point</param>
        /// <param name="target">Target point</param>
        /// <returns>Heading to target point</returns>
        public static ushort GetHeading(Coordinate origin, Coordinate target)
        {
            float dx = target.X - origin.X;
            float dy = target.Y - origin.Y;

            float heading = (float)Math.Atan2(-dx, dy) * RADIAN_TO_HEADING;

            if (heading < 0)
                heading += 4096;

            return (ushort)heading;
        }

        public static float GetAngle(GameObject origin, GameObject target)
            => GetAngle(origin.Coordinate, origin.Heading, target.Coordinate);
        public static float GetAngle(Coordinate origin, ushort originHeading, Coordinate target)
        {
            float headingDifference = (GetHeading(origin, target) & 0xFFF) - (originHeading & 0xFFF);

            if (headingDifference < 0)
                headingDifference += 4096.0f;

            return (headingDifference * 360.0f / 4096.0f);
        }
        
        public static int GCD(int[] numbers)
        {
            return numbers.Aggregate(GCD);
        }

        public static int GCD(int a, int b)
        {
            return b == 0 ? a : GCD(b, a % b);
        }

        public static Coordinate GetPointFromHeading(GameObject origin, float distance)
            => GetPointFromHeading(origin.Coordinate, origin.Heading, distance);
        public static Coordinate GetPointFromHeading(Coordinate origin, ushort heading, float distance)
        {
            float angle = heading * HEADING_TO_RADIAN;
            float targetX = origin.X - ((float)Math.Sin(angle) * distance);
            float targetY = origin.Y + ((float)Math.Cos(angle) * distance);

            return Coordinate.Create(int.Max((int)targetX, 0), int.Max((int)targetY, 0));
        }

        /// <summary>
        /// Get the distance without Z between two points
        /// </summary>
        /// <remarks>
        /// If you don't actually need the distance value, it is faster
        /// to use IsWithinRadius (since it avoids the square root calculation)
        /// </remarks>
        /// <param name="b">Source point</param>
        /// <param name="a">Target point</param>
        /// <returns>Distance to point</returns>
        public static float GetDistance2D(Coordinate a, Coordinate b) => (float)(b - a).Length2D;
        
        public static float GetDistance2D(GameObject a, GameObject b)
        {
            if (a.CurrentRegion != b.CurrentRegion)
                return float.MaxValue;
            return GetDistance2D(a.Coordinate, b.Coordinate);
        }
        
        public static float GetDistance2DSquared(Coordinate a, Coordinate b)
        {
            float dX = b.X - a.X;
            float dY = b.Y - a.Y;
            return dX * dX + dY * dY;
        }
        
        public static float GetDistance2DSquared(Position a, Position b)
        {
            if (a.RegionID != b.RegionID)
                return float.MaxValue;
            float dX = b.X - a.X;
            float dY = b.Y - a.Y;
            return dX * dX + dY * dY;
        }
        
        public static float GetDistance2DSquared(GameObject a, GameObject b)
        {
            if (a.CurrentRegion != b.CurrentRegion)
                return float.MaxValue;
            return GetDistance2DSquared(a.Coordinate, b.Coordinate);
        }

        public static float GetDistance(Coordinate a, Coordinate b) => (float)(b - a).Length;

        public static float GetDistance(GameObject a, GameObject b)
        {
            if (a.CurrentRegion != b.CurrentRegion)
                return float.MaxValue;
            return GetDistance(a.Coordinate, b.Coordinate);
        }

        public static float GetDistanceSquared(Coordinate a, Coordinate b)
        {
            float dX = b.X - a.X;
            float dY = b.Y - a.Y;
            float dZ = b.Z - a.Z;
            return dX * dX + dY * dY + dZ * dZ;
        }

        public static float GetDistanceSquared(GameObject a, GameObject b)
        {
            if (a.CurrentRegion != b.CurrentRegion)
                return float.MaxValue;
            return GetDistanceSquared(a.Coordinate, b.Coordinate);
        }

        public static bool IsWithinRadius(GameObject source, GameObject target, float distance)
        {
            if (source.CurrentRegion != target.CurrentRegion)
                return false;
            return GetDistanceSquared(source.Coordinate, target.Coordinate) <= distance * distance;
        }
        
        public static bool IsWithinRadius(GameObject source, Coordinate target, float distance)
            => GetDistanceSquared(source.Coordinate, target) <= distance * distance;
        
        public static bool IsWithinRadius(Coordinate source, Coordinate target, float distance)
            => GetDistanceSquared(source, target) <= distance * distance;

        public static bool IsWithinRadius2D(GameObject source, GameObject target, float distance)
        {
            if (source.CurrentRegion != target.CurrentRegion)
                return false;
            return GetDistance2DSquared(source.Coordinate, target.Coordinate) <= distance * distance;
        }
        
        public static bool IsWithinRadius2D(GameObject source, Coordinate target, float distance)
            => GetDistance2DSquared(source.Coordinate, target) <= distance * distance;
        
        public static bool IsWithinRadius2D(Coordinate source, Coordinate target, float distance)
            => GetDistance2DSquared(source, target) <= distance * distance;
        
        public static bool IsWithinRadius2D(Position source, Position target, float distance)
            => GetDistance2DSquared(source, target) <= distance * distance;
    }
}