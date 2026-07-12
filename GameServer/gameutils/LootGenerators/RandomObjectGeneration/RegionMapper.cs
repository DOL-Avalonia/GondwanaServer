using System;
using System.Collections.Generic;

namespace DOL.GS
{
    /// <summary>
    /// Defines the main progression and difficulty categories for item budgeting.
    /// </summary>
    public enum eRegionCategory
    {
        ClassicOverworld,
        ClassicDungeons,
        AtlantisOverworld,
        AtlantisDungeons,
        Catacombs,
        DeepCatacombs,
        MythicalZones,
        Battlegrounds,
        Restricted
    }

    /// <summary>
    /// Defines the thematic environments that dictate prioritized resists and damage types.
    /// </summary>
    public enum eRegionEnvironment
    {
        Global,
        CryptsAndUndead,
        CavesAndNature,
        VolcanicAndEarth,
        GlacialAndAquatic,
        MagicalAndEthereal,
        DemonicAndAbyss,
        BeastAndGoblinoid
    }

    /// <summary>
    /// Maps specific DAoC Region IDs to their Categories and provides Environmental profiles.
    /// </summary>
    public static class RegionMapper
    {
        private static readonly Dictionary<int, eRegionCategory> CategoryMap = new Dictionary<int, eRegionCategory>();
        private static readonly Dictionary<int, eRegionEnvironment> EnvironmentMap = new Dictionary<int, eRegionEnvironment>();

        static RegionMapper()
        {
            // ==========================================
            // 1. REGION CATEGORY MAPPING
            // ==========================================

            MapCategory(eRegionCategory.Restricted,
                2, 10, 101, 102, 201, 202, 360, 394, 395);

            MapCategory(eRegionCategory.ClassicOverworld,
                1, 27, 51, 100, 151, 181, 200, 342, 343, 344, 497, 498, 499);

            MapCategory(eRegionCategory.ClassicDungeons,
                20, 21, 22, 23, 24, 48, 50, 60, 61, 62, 125, 126, 127, 128, 129,
                150, 160, 161, 180, 190, 191, 192, 193, 194, 220, 221, 222, 223,
                224, 231, 232, 379, 382, 383, 427, 428, 431, 432, 441, 444, 445,
                448, 449, 477, 478, 481, 482, 485, 486);
            MapCategoryRange(eRegionCategory.ClassicDungeons, 256, 258);
            MapCategoryRange(eRegionCategory.ClassicDungeons, 278, 298);
            MapCategoryRange(eRegionCategory.ClassicDungeons, 300, 324);
            MapCategoryRange(eRegionCategory.ClassicDungeons, 386, 388);
            MapCategoryRange(eRegionCategory.ClassicDungeons, 400, 424);
            MapCategoryRange(eRegionCategory.ClassicDungeons, 450, 469);
            MapCategoryRange(eRegionCategory.ClassicDungeons, 471, 474);

            MapCategory(eRegionCategory.AtlantisOverworld,
                30, 70, 71, 72, 73, 130);

            MapCategory(eRegionCategory.AtlantisDungeons,
                35, 36, 37, 40, 45, 46, 47, 78, 79, 80, 83, 88, 89, 90,
                135, 136, 137, 140, 145, 146, 147);

            MapCategory(eRegionCategory.Catacombs,
                58, 59, 63, 65, 66, 67, 68, 69, 93, 94, 95, 96, 97, 98, 99,
                109, 148, 149, 162, 188, 189, 195, 196, 197, 226, 227, 228, 229,
                243, 330, 334, 335);

            MapCategory(eRegionCategory.DeepCatacombs,
                49, 92, 233, 244, 249, 325, 326, 327, 328, 329, 331, 332, 333,
                336, 337, 338, 339, 340, 341, 380, 381, 384, 385, 389, 390, 391,
                392, 393, 396, 397, 398, 399, 425, 426, 429, 430, 433,
                434, 435, 436, 437, 438, 439, 440, 442, 443, 446, 447, 470, 475,
                476, 479, 480, 483, 484, 487, 488, 489, 491, 492, 494, 495, 496);
            MapCategoryRange(eRegionCategory.DeepCatacombs, 345, 359);
            MapCategoryRange(eRegionCategory.DeepCatacombs, 361, 378);

            MapCategory(eRegionCategory.MythicalZones,
                3, 91, 123, 124, 198, 199, 230, 245, 247);

            MapCategory(eRegionCategory.Battlegrounds,
                163, 165, 166, 167, 168, 169, 234, 235, 236, 237, 238, 239, 240, 241, 242);

            // ==========================================
            // 2. ENVIRONMENT MAPPING
            // ==========================================

            // Global (Overworlds, BGs, Cities, Tajendi, Art Test Zones)
            MapEnvironment(eRegionEnvironment.Global,
                1, 2, 10, 27, 30, 51, 70, 71, 72, 73, 100, 101, 102, 130, 151,
                163, 165, 166, 167, 168, 169, 181, 200, 201, 202, 230, 231, 232,
                234, 235, 236, 237, 238, 239, 240, 241, 242, 342, 343, 344,
                497, 498, 499);

            MapEnvironment(eRegionEnvironment.CryptsAndUndead,
                20, 21, 23, 45, 65, 67, 68, 88, 97, 128, 145, 162, 221, 229, 333, 349, 350, 353, 356, 357,
                374, 375, 379, 382, 383, 384, 393, 427, 428, 431, 442, 443);
            MapEnvironmentRange(eRegionEnvironment.CryptsAndUndead, 281, 283);
            MapEnvironmentRange(eRegionEnvironment.CryptsAndUndead, 293, 298);
            MapEnvironmentRange(eRegionEnvironment.CryptsAndUndead, 305, 309);
            MapEnvironmentRange(eRegionEnvironment.CryptsAndUndead, 386, 388);
            MapEnvironmentRange(eRegionEnvironment.CryptsAndUndead, 400, 409);
            MapEnvironmentRange(eRegionEnvironment.CryptsAndUndead, 415, 424);
            MapEnvironmentRange(eRegionEnvironment.CryptsAndUndead, 450, 454);

            MapEnvironment(eRegionEnvironment.CavesAndNature,
                22, 58, 62, 66, 96, 125, 129, 150, 191, 222, 224, 247, 325, 326, 327,
                328, 329, 341, 354, 358, 361, 362, 432, 433, 441, 444, 445, 446,
                448, 449, 475, 476, 477, 478, 479, 481);
            MapEnvironmentRange(eRegionEnvironment.CavesAndNature, 278, 280);
            MapEnvironmentRange(eRegionEnvironment.CavesAndNature, 287, 292);
            MapEnvironmentRange(eRegionEnvironment.CavesAndNature, 300, 304);
            MapEnvironmentRange(eRegionEnvironment.CavesAndNature, 315, 324);
            MapEnvironmentRange(eRegionEnvironment.CavesAndNature, 455, 469);

            MapEnvironment(eRegionEnvironment.VolcanicAndEarth,
                24, 46, 48, 59, 89, 99, 123, 124, 146, 189, 198, 199, 220, 226,
                227, 228, 339, 435, 436, 437, 439, 440, 482, 485, 486, 488);
            MapEnvironmentRange(eRegionEnvironment.VolcanicAndEarth, 256, 258);
            MapEnvironmentRange(eRegionEnvironment.VolcanicAndEarth, 410, 414);
            MapEnvironmentRange(eRegionEnvironment.VolcanicAndEarth, 471, 474);

            MapEnvironment(eRegionEnvironment.GlacialAndAquatic,
                35, 36, 63, 78, 79, 135, 136, 160, 161, 223, 429, 430);

            MapEnvironment(eRegionEnvironment.MagicalAndEthereal,
                3, 37, 40, 47, 50, 80, 83, 90, 91, 92, 93, 94, 137,
                140, 147, 149, 192, 193, 194, 233, 245, 355, 360, 363,
                364, 368, 373, 378, 389, 391, 392, 394, 395, 398, 399, 425, 426,
                447, 495);

            MapEnvironment(eRegionEnvironment.DemonicAndAbyss,
                60, 69, 98, 180, 188, 190, 195, 196, 197, 244, 249, 336, 337, 338, 340, 347,
                348, 351, 352, 380, 385, 390, 396, 397, 434, 484, 487, 489, 491,
                492, 496);

            MapEnvironment(eRegionEnvironment.BeastAndGoblinoid,
                49, 61, 95, 109, 126, 127, 148, 243, 330, 331, 332, 334, 335,
                345, 346, 359, 365, 366, 367, 369, 370, 371, 372, 376, 377, 381,
                438, 470, 480, 483, 494);
            MapEnvironmentRange(eRegionEnvironment.BeastAndGoblinoid, 284, 286);
            MapEnvironmentRange(eRegionEnvironment.BeastAndGoblinoid, 310, 314);
        }

        #region Helpers to Map Category & Environment
        private static void MapCategory(eRegionCategory category, params int[] regionIds)
        {
            foreach (int id in regionIds)
                CategoryMap[id] = category;
        }

        private static void MapCategoryRange(eRegionCategory category, int startId, int endId)
        {
            for (int i = startId; i <= endId; i++)
                CategoryMap[i] = category;
        }

        private static void MapEnvironment(eRegionEnvironment env, params int[] regionIds)
        {
            foreach (int id in regionIds)
                EnvironmentMap[id] = env;
        }

        private static void MapEnvironmentRange(eRegionEnvironment env, int startId, int endId)
        {
            for (int i = startId; i <= endId; i++)
                EnvironmentMap[i] = env;
        }
        #endregion

        /// <summary>
        /// Retrieves the category for the provided RegionID. Defaults to ClassicOverworld.
        /// </summary>
        public static eRegionCategory GetCategoryFromRegionID(int regionId)
        {
            if (CategoryMap.TryGetValue(regionId, out eRegionCategory category))
                return category;

            return eRegionCategory.ClassicOverworld;
        }

        /// <summary>
        /// Retrieves the specific environment for the RegionID. Defaults to Global.
        /// </summary>
        public static eRegionEnvironment GetEnvironmentFromRegionID(int regionId)
        {
            if (EnvironmentMap.TryGetValue(regionId, out eRegionEnvironment environment))
                return environment;

            return eRegionEnvironment.Global;
        }

        /// <summary>
        /// Returns prioritized defensive Resists based on environment.
        /// </summary>
        public static List<eProperty> GetPrioritizedResists(eRegionEnvironment env)
        {
            switch (env)
            {
                case eRegionEnvironment.CryptsAndUndead:
                    return new List<eProperty> { eProperty.Resist_Spirit, eProperty.Resist_Cold, eProperty.Resist_Matter };

                case eRegionEnvironment.CavesAndNature:
                    return new List<eProperty> { eProperty.Resist_Body, eProperty.Resist_Heat, eProperty.Resist_Energy };

                case eRegionEnvironment.VolcanicAndEarth:
                    return new List<eProperty> { eProperty.Resist_Heat, eProperty.Resist_Matter, eProperty.Resist_Crush };

                case eRegionEnvironment.GlacialAndAquatic:
                    return new List<eProperty> { eProperty.Resist_Cold, eProperty.Resist_Energy, eProperty.Resist_Slash };

                case eRegionEnvironment.MagicalAndEthereal:
                    return new List<eProperty> { eProperty.Resist_Spirit, eProperty.Resist_Energy, eProperty.Resist_Natural };

                case eRegionEnvironment.DemonicAndAbyss:
                    return new List<eProperty> { eProperty.Resist_Heat, eProperty.Resist_Cold, eProperty.Resist_Body, eProperty.Resist_Spirit };

                case eRegionEnvironment.BeastAndGoblinoid:
                    return new List<eProperty> { eProperty.Resist_Crush, eProperty.Resist_Slash, eProperty.Resist_Thrust };

                case eRegionEnvironment.Global:
                default:
                    return new List<eProperty>();
            }
        }

        /// <summary>
        /// Returns prioritized offensive Damage Types for generated weapons based on environment.
        /// </summary>
        public static List<eDamageType> GetPrioritizedDamageTypes(eRegionEnvironment env)
        {
            switch (env)
            {
                case eRegionEnvironment.CryptsAndUndead:
                    return new List<eDamageType> { eDamageType.Matter, eDamageType.Cold };

                case eRegionEnvironment.CavesAndNature:
                    return new List<eDamageType> { eDamageType.Body, eDamageType.Thrust };

                case eRegionEnvironment.VolcanicAndEarth:
                    return new List<eDamageType> { eDamageType.Heat, eDamageType.Crush };

                case eRegionEnvironment.GlacialAndAquatic:
                    return new List<eDamageType> { eDamageType.Cold, eDamageType.Thrust };

                case eRegionEnvironment.MagicalAndEthereal:
                    return new List<eDamageType> { eDamageType.Energy, eDamageType.Spirit, eDamageType.Natural };

                case eRegionEnvironment.DemonicAndAbyss:
                    return new List<eDamageType> { eDamageType.Heat, eDamageType.Body, eDamageType.Natural };

                case eRegionEnvironment.BeastAndGoblinoid:
                    return new List<eDamageType> { eDamageType.Slash, eDamageType.Crush, eDamageType.Thrust };

                case eRegionEnvironment.Global:
                default:
                    return new List<eDamageType>();
            }
        }
    }
}