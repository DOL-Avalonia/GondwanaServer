using System;
using System.Collections.Generic;

namespace DOL.GS
{
    public enum ConColor
    {
        Unknown = int.MinValue,
        Grey = -3,
        Green = -2,
        Blue = -1,
        Yellow = 0,
        Orange = 1,
        Red = 2,
        Purple = 3
    }

    /// <summary>Shared helpers for the pre-computed con-level grids.</summary>
    public static class ConLevelUtility
    {
        public const int MAX_LEVEL = 128;

        public static short[,] BuildGrid(List<(short ConLevel, short Span)[]> data)
        {
            short[,] grid = new short[MAX_LEVEL, MAX_LEVEL];
            var lastKnownData = data[^1];

            for (int i = data.Count, j = 1; i < MAX_LEVEL; i++, j++)
            {
                data.Add(
                [
                    (-3, (short)(lastKnownData[0].Span + j)),
                    (-2, lastKnownData[1].Span),
                    (-1, lastKnownData[2].Span),
                    (0, lastKnownData[3].Span),
                    (1, lastKnownData[4].Span),
                    (2, lastKnownData[5].Span)
                ]);
            }

            for (int i = 0; i < data.Count; i++)
            {
                int index = 0;
                foreach (var (conLevel, span) in data[i])
                    for (int j = 0; j < span && index < MAX_LEVEL; j++, index++)
                        grid[i, index] = conLevel;
                for (; index < MAX_LEVEL; index++)
                    grid[i, index] = 3;
            }

            return grid;
        }

        public static int GetConLevel(short[,] grid, int userLevel, int targetLevel)
        {
            userLevel = Math.Clamp(userLevel, 0, grid.GetLength(0) - 1);
            targetLevel = Math.Clamp(targetLevel, 0, grid.GetLength(1) - 1);
            return grid[userLevel, targetLevel];
        }

        public static ConColor GetConColor(int conLevel)
            => conLevel is < (int)ConColor.Grey or > (int)ConColor.Purple ? ConColor.Unknown : (ConColor)conLevel;
    }

    /// <summary>Live-accurate character con colours (OpenDAoC tables). Display / comparison only on Gondwana.</summary>
    public static class CharacterConLevelGrid
    {
        private static readonly short[,] _conLevels;

        static CharacterConLevelGrid()
        {
            List<(short, short)[]> data =
            [
                [(0,1),(1,1),(2,1)], [(-1,1),(0,1),(1,1),(2,1)], [(-2,1),(-1,1),(0,1),(1,1),(2,1)],
                [(-3,1),(-2,1),(-1,1),(0,1),(1,1),(2,1)], [(-3,2),(-2,1),(-1,1),(0,1),(1,1),(2,1)], [(-3,3),(-2,1),(-1,1),(0,1),(1,1),(2,1)],
                [(-3,4),(-2,1),(-1,1),(0,1),(1,1),(2,1)], [(-3,5),(-2,1),(-1,1),(0,1),(1,1),(2,1)], [(-3,6),(-2,1),(-1,1),(0,1),(1,1),(2,1)],
                [(-3,7),(-2,1),(-1,1),(0,1),(1,1),(2,1)], [(-3,7),(-2,1),(-1,2),(0,1),(1,1),(2,2)], [(-3,7),(-2,1),(-1,2),(0,2),(1,2),(2,2)],
                [(-3,7),(-2,2),(-1,2),(0,2),(1,2),(2,2)], [(-3,8),(-2,2),(-1,2),(0,2),(1,2),(2,2)], [(-3,9),(-2,2),(-1,2),(0,2),(1,2),(2,2)],
                [(-3,10),(-2,2),(-1,2),(0,2),(1,2),(2,2)], [(-3,11),(-2,2),(-1,2),(0,2),(1,2),(2,2)], [(-3,12),(-2,2),(-1,2),(0,2),(1,2),(2,2)],
                [(-3,13),(-2,2),(-1,2),(0,2),(1,2),(2,2)], [(-3,14),(-2,2),(-1,2),(0,2),(1,2),(2,2)], [(-3,14),(-2,2),(-1,3),(0,2),(1,2),(2,3)],
                [(-3,14),(-2,2),(-1,3),(0,3),(1,3),(2,3)], [(-3,14),(-2,3),(-1,3),(0,3),(1,3),(2,3)], [(-3,15),(-2,3),(-1,3),(0,3),(1,3),(2,3)],
                [(-3,16),(-2,3),(-1,3),(0,3),(1,3),(2,3)], [(-3,17),(-2,3),(-1,3),(0,3),(1,3),(2,3)], [(-3,18),(-2,3),(-1,3),(0,3),(1,3),(2,3)],
                [(-3,19),(-2,3),(-1,3),(0,3),(1,3),(2,3)], [(-3,20),(-2,3),(-1,3),(0,3),(1,3),(2,3)], [(-3,21),(-2,3),(-1,3),(0,3),(1,3),(2,3)],
                [(-3,22),(-2,3),(-1,3),(0,3),(1,3),(2,3)], [(-3,23),(-2,3),(-1,3),(0,3),(1,3),(2,3)], [(-3,24),(-2,3),(-1,3),(0,3),(1,3),(2,3)],
                [(-3,25),(-2,3),(-1,3),(0,3),(1,3),(2,3)], [(-3,26),(-2,3),(-1,3),(0,3),(1,3),(2,3)], [(-3,26),(-2,3),(-1,3),(0,4),(1,4),(2,3)],
                [(-3,26),(-2,3),(-1,3),(0,5),(1,5),(2,4)], [(-3,26),(-2,4),(-1,3),(0,5),(1,5),(2,5)], [(-3,26),(-2,4),(-1,4),(0,5),(1,5),(2,5)],
                [(-3,26),(-2,4),(-1,5),(0,5),(1,5),(2,5)], [(-3,26),(-2,5),(-1,5),(0,5),(1,5),(2,5)], [(-3,27),(-2,5),(-1,5),(0,5),(1,5),(2,5)],
                [(-3,28),(-2,5),(-1,5),(0,5),(1,5),(2,5)], [(-3,29),(-2,5),(-1,5),(0,5),(1,5),(2,5)], [(-3,30),(-2,5),(-1,5),(0,5),(1,5),(2,5)],
                [(-3,31),(-2,5),(-1,5),(0,5),(1,5),(2,5)], [(-3,32),(-2,5),(-1,5),(0,5),(1,5),(2,5)], [(-3,33),(-2,5),(-1,5),(0,5),(1,5),(2,5)],
                [(-3,34),(-2,5),(-1,5),(0,5),(1,5),(2,5)], [(-3,35),(-2,5),(-1,5),(0,5),(1,5),(2,5)], [(-3,36),(-2,5),(-1,5),(0,5),(1,5),(2,5)]
            ];
            _conLevels = ConLevelUtility.BuildGrid(data);
        }

        public static int GetConLevel(int userLevel, int targetLevel) => ConLevelUtility.GetConLevel(_conLevels, userLevel, targetLevel);
        public static ConColor GetConColor(int conLevel) => ConLevelUtility.GetConColor(conLevel);
    }

    /// <summary>Live-accurate item con colours (OpenDAoC tables).</summary>
    public static class ItemConLevelGrid
    {
        private static readonly short[,] _conLevels;

        static ItemConLevelGrid()
        {
            List<(short, short)[]> data =
            [
                [(0,2),(1,1),(2,1)], [(0,3),(1,2),(2,2)], [(-1,1),(0,3),(1,2),(2,2)], [(-1,2),(0,3),(1,2),(2,2)],
                [(-2,1),(-1,2),(0,3),(1,2),(2,2)], [(-2,2),(-1,2),(0,3),(1,2),(2,2)], [(-3,1),(-2,2),(-1,2),(0,3),(1,2),(2,2)],
                [(-3,2),(-2,2),(-1,2),(0,3),(1,2),(2,2)], [(-3,3),(-2,2),(-1,2),(0,3),(1,2),(2,2)], [(-3,4),(-2,2),(-1,2),(0,3),(1,2),(2,2)],
                [(-3,4),(-2,2),(-1,3),(0,3),(1,2),(2,3)], [(-3,4),(-2,2),(-1,3),(0,4),(1,3),(2,3)], [(-3,4),(-2,3),(-1,3),(0,4),(1,3),(2,3)],
                [(-3,5),(-2,3),(-1,3),(0,4),(1,3),(2,3)], [(-3,6),(-2,3),(-1,3),(0,4),(1,3),(2,3)], [(-3,7),(-2,3),(-1,3),(0,4),(1,3),(2,3)],
                [(-3,8),(-2,3),(-1,3),(0,4),(1,3),(2,3)], [(-3,9),(-2,3),(-1,3),(0,4),(1,3),(2,3)], [(-3,10),(-2,3),(-1,3),(0,4),(1,3),(2,3)],
                [(-3,11),(-2,3),(-1,3),(0,4),(1,3),(2,3)], [(-3,11),(-2,3),(-1,4),(0,4),(1,3),(2,4)], [(-3,11),(-2,3),(-1,4),(0,5),(1,4),(2,4)],
                [(-3,11),(-2,4),(-1,4),(0,5),(1,4),(2,4)], [(-3,12),(-2,4),(-1,4),(0,5),(1,4),(2,4)], [(-3,13),(-2,4),(-1,4),(0,5),(1,4),(2,4)],
                [(-3,14),(-2,4),(-1,4),(0,5),(1,4),(2,4)], [(-3,15),(-2,4),(-1,4),(0,5),(1,4),(2,4)], [(-3,16),(-2,4),(-1,4),(0,5),(1,4),(2,4)],
                [(-3,17),(-2,4),(-1,4),(0,5),(1,4),(2,4)], [(-3,18),(-2,4),(-1,4),(0,5),(1,4),(2,4)], [(-3,19),(-2,4),(-1,4),(0,5),(1,4),(2,4)],
                [(-3,20),(-2,4),(-1,4),(0,5),(1,4),(2,4)], [(-3,21),(-2,4),(-1,4),(0,5),(1,4),(2,4)], [(-3,22),(-2,4),(-1,4),(0,5),(1,4),(2,4)],
                [(-3,23),(-2,4),(-1,4),(0,5),(1,4),(2,4)], [(-3,23),(-2,4),(-1,4),(0,6),(1,5),(2,4)], [(-3,23),(-2,4),(-1,4),(0,7),(1,6),(2,5)],
                [(-3,23),(-2,5),(-1,4),(0,7),(1,6),(2,6)], [(-3,23),(-2,5),(-1,5),(0,7),(1,6),(2,6)], [(-3,23),(-2,5),(-1,6),(0,7),(1,6),(2,6)],
                [(-3,23),(-2,6),(-1,6),(0,7),(1,6),(2,6)], [(-3,24),(-2,6),(-1,6),(0,7),(1,6),(2,6)], [(-3,25),(-2,6),(-1,6),(0,7),(1,6),(2,6)],
                [(-3,26),(-2,6),(-1,6),(0,7),(1,6),(2,6)], [(-3,27),(-2,6),(-1,6),(0,7),(1,6),(2,6)], [(-3,28),(-2,6),(-1,6),(0,7),(1,6),(2,6)],
                [(-3,29),(-2,6),(-1,6),(0,7),(1,6),(2,6)], [(-3,30),(-2,6),(-1,6),(0,7),(1,6),(2,6)], [(-3,31),(-2,6),(-1,6),(0,7),(1,6),(2,6)],
                [(-3,32),(-2,6),(-1,6),(0,7),(1,6),(2,6)], [(-3,33),(-2,6),(-1,6),(0,7),(1,6),(2,6)]
            ];
            _conLevels = ConLevelUtility.BuildGrid(data);
        }

        public static int GetConLevel(int userLevel, int targetLevel) => ConLevelUtility.GetConLevel(_conLevels, userLevel, targetLevel);
        public static ConColor GetConColor(int conLevel) => ConLevelUtility.GetConColor(conLevel);
    }

    /// <summary>
    /// Gondwana's own con math (the classic DOL formula) pre-computed once for every level pair,
    /// plus the derived tables used by XP/loot (grey) and by the Task system (tiers).
    /// GAMEPLAY VALUES ARE IDENTICAL to the old if/else code - this is a pure cache.
    /// </summary>
    public static class LegacyConLevelGrid
    {
        public const int MAX_LEVEL = ConLevelUtility.MAX_LEVEL;

        public static readonly int[] NoXPForLevel =
        {
            -3,-2,-1,0,1,2,3,4,5,6,6,6,6,7,8,9,10,11,12,13,13,13,13,14,15,16,17,18,19,20,
            21,22,23,24,25,25,25,25,25,25,25,26,27,28,29,30,31,32,33,34,35
        };

        private static readonly double[,] _con = new double[MAX_LEVEL, MAX_LEVEL];
        private static readonly bool[,] _grey = new bool[MAX_LEVEL, MAX_LEVEL];
        private static readonly byte[,] _npcTaskTier = new byte[MAX_LEVEL, MAX_LEVEL];
        private static readonly byte[,] _pvpTaskTier = new byte[MAX_LEVEL, MAX_LEVEL];
        private static readonly sbyte[,] _color = new sbyte[MAX_LEVEL, MAX_LEVEL];

        static LegacyConLevelGrid()
        {
            for (int level = 0; level < MAX_LEVEL; level++)
            {
                for (int compare = 0; compare < MAX_LEVEL; compare++)
                {
                    double con = Compute(level, compare);
                    bool grey = ComputeGrey(level, compare, con);
                    _con[level, compare] = con;
                    _grey[level, compare] = grey;
                    _npcTaskTier[level, compare] = (byte)ComputeNpcTaskTier(con);
                    _pvpTaskTier[level, compare] = (byte)ComputePvpTaskTier(compare - level);
                    _color[level, compare] = (sbyte)ComputeColor(con, grey);
                }
            }
        }

        private static bool InGrid(int a, int b) => (uint)a < MAX_LEVEL && (uint)b < MAX_LEVEL;

        /// <summary>Exact replacement of GameObject.GetConLevel(level, compareLevel).</summary>
        public static double GetConLevel(int level, int compareLevel)
            => InGrid(level, compareLevel) ? _con[level, compareLevel] : Compute(level, compareLevel);

        /// <summary>Exact replacement of GameLiving.IsObjectGreyCon(source, target) (no XP / no loot / no task).</summary>
        public static bool IsGreyCon(int sourceLevel, int targetLevel)
            => InGrid(sourceLevel, targetLevel) ? _grey[sourceLevel, targetLevel] : ComputeGrey(sourceLevel, targetLevel, Compute(sourceLevel, targetLevel));

        /// <summary>0 = grey, 1 = green, 2 = blue, 3 = yellow or better (PvE rule of TaskSafeguardCheck).</summary>
        public static int GetNpcTaskTier(int playerLevel, int enemyLevel)
            => InGrid(playerLevel, enemyLevel) ? _npcTaskTier[playerLevel, enemyLevel] : ComputeNpcTaskTier(Compute(playerLevel, enemyLevel));

        /// <summary>0..3 by raw level difference (PvP rule of TaskSafeguardCheck).</summary>
        public static int GetPvPTaskTier(int playerLevel, int enemyLevel)
            => InGrid(playerLevel, enemyLevel) ? _pvpTaskTier[playerLevel, enemyLevel] : ComputePvpTaskTier(enemyLevel - playerLevel);

        /// <summary>Colour derived from the legacy value (rounded), grey when no XP would be given.</summary>
        public static ConColor GetConColor(int level, int compareLevel)
            => InGrid(level, compareLevel) ? (ConColor)_color[level, compareLevel] : (ConColor)ComputeColor(Compute(level, compareLevel), IsGreyCon(level, compareLevel));

        // ---- the original math, kept for out-of-grid levels (e.g. portal keep guards at level 255) ----
        public static double Compute(int level, int compareLevel)
        {
            int constep = Math.Max(1, (level + 9) / 10);
            double stepping = 1.0 / constep;
            int leveldiff = level - compareLevel;
            return 0 - leveldiff * stepping;
        }

        private static bool ComputeGrey(int sourceLevel, int targetLevel, double con)
        {
            if (sourceLevel >= 0 && sourceLevel < NoXPForLevel.Length)
                return targetLevel <= NoXPForLevel[sourceLevel];
            return con <= -3;
        }

        private static int ComputeNpcTaskTier(double con) => con >= 0.01 ? 3 : con >= -0.99 ? 2 : con >= -1.99 ? 1 : 0;
        private static int ComputePvpTaskTier(int diff) => diff <= -5 ? 0 : diff <= -2 ? 1 : diff <= 2 ? 2 : 3;

        private static int ComputeColor(double con, bool grey)
        {
            if (grey) return (int)ConColor.Grey;
            int rounded = (int)Math.Round(con, MidpointRounding.AwayFromZero);
            return Math.Clamp(rounded, (int)ConColor.Green, (int)ConColor.Purple);
        }
    }

    public static class ConLevelExtensions
    {
        /// <summary>Gondwana colour (legacy math) of target as seen by source.</summary>
        public static ConColor GetConColor(this GameObject source, GameObject target)
            => LegacyConLevelGrid.GetConColor(source.EffectiveLevel, target.EffectiveLevel);

        /// <summary>Live-server colour (OpenDAoC tables) of target as seen by source.</summary>
        public static ConColor GetLiveConColor(this GameObject source, GameObject target)
            => CharacterConLevelGrid.GetConColor(CharacterConLevelGrid.GetConLevel(source.EffectiveLevel, target.EffectiveLevel));
    }
}