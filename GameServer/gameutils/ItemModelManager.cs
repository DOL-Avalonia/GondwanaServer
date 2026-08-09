using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;

namespace DOL.GS
{
    // Added all class patterns to the enum
    public enum PatternType
    {
        None, Possessed, Good, Corrupt, Minotaur, Oceanus, Stygia, Volcanus, Aerus,
        ClassAnimist, ClassArmsman, ClassBainshee, ClassBard, ClassBerserker, ClassBlademaster,
        ClassBonedancer, ClassCabalist, ClassChampion, ClassCleric, ClassDruid, ClassEldritch,
        ClassEnchanter, ClassFriar, ClassHealer, ClassHeretic, ClassHero, ClassHunter,
        ClassInfiltrator, ClassMaulerAlb, ClassMaulerMid, ClassMaulerHib, ClassMentalist,
        ClassMercenary, ClassMinstrel, ClassNecromancer, ClassNightshade, ClassPaladin, ClassRanger,
        ClassReaver, ClassRunemaster, ClassSavage, ClassScout, ClassShadowblade, ClassShaman,
        ClassSkald, ClassSorcerer, ClassSpiritmaster, ClassThane, ClassTheurgist, ClassValewalker,
        ClassValkyrie, ClassVampiir, ClassWarden, ClassWarlock, ClassWarrior, ClassWizard, ClassOccultist
    }

    public enum eMaskType
    {
        Random, Eye, Horned, Slitted, Skull, Plague, Beast, Devour, Satyr, Death,
        OrnatedPlague, OrnatedBeast, OrnatedSatyr, OrnatedSkull, Yule, Demon,
        Pumpkin, Angel, Midona, Tentacled, Headless, Krampus,
        Hothead, Sunburst, HeadOrbit, EyesFire, EyesBlue, EyesGreen
    }

    public static class ItemModelManager
    {
        #region Mask & Skeleton Logic

        public static string GetRandomMaskType()
        {
            string[] types = { "eye", "horned", "slitted", "skull", "plague", "beast", "devour", "satyr", "death", "ornatedplague", "ornatedbeast", "ornatedsatyr", "ornatedskull", "yule", "demon", "pumpkin", "angel", "midona", "tentacled", "hothead", "sunburst", "headorbit", "eyesfire", "eyesblue", "eyesgreen" };
            return types[Util.Random(types.Length - 1)];
        }

        public static int GetMaskEffect(string maskTypeStr)
        {
            if (string.IsNullOrEmpty(maskTypeStr)) return 0;

            switch (maskTypeStr.ToLower())
            {
                case "pumpkin": return 797;
                case "tentacled": return 798;
                case "hothead": return 790;
                case "sunburst": return 791;
                case "headorbit": return 792;
                case "eyesfire": return 794;
                case "eyesblue": return 795;
                case "eyesgreen": return 796;
                default: return 0;
            }
        }

        public static int GetNPCBodyCategory(int model)
        {
            // Mino (Return 3)
            if ((model >= 1395 && model <= 1430) || (model >= 1564 && model <= 1566)) return 3;
            
            // Midgard (Return 1)
            if ((model >= 153 && model <= 240) || (model >= 420 && model <= 439) || (model >= 503 && model <= 534) || (model >= 773 && model <= 804) || (model >= 832 && model <= 839) || model == 958 || model == 959 || model == 964 || model == 965 || (model >= 1972 && model <= 1973) || model == 2013 || model == 2016 || (model >= 2312 && model <= 2313) || (model >= 2088 && model <= 2089) || (model >= 137 && model <= 152) || (model >= 201 && model <= 211) || (model >= 511 && model <= 518) || (model >= 1970 && model <= 1971) || (model >= 2310 && model <= 2311) || (model >= 2086 && model <= 2087) || (model >= 185 && model <= 200) || (model >= 231 && model <= 240) || (model >= 519 && model <= 526) || model == 1759 || (model >= 1976 && model <= 1977) || (model >= 2316 && model <= 2317) || (model >= 2092 && model <= 2093) || (model >= 1051 && model <= 1074) || (model >= 1123 && model <= 1146) || (model >= 1980 && model <= 1981) || (model >= 2218 && model <= 2219) || (model >= 169 && model <= 184) || (model >= 221 && model <= 230) || (model >= 527 && model <= 534) || model == 1625 || (model >= 1974 && model <= 1975) || (model >= 2314 && model <= 2315) || (model >= 2090 && model <= 2091)) return 1;

            // Hibernia (Return 2)
            if ((model >= 302 && model <= 317) || (model >= 360 && model <= 369) || model == 390 || (model >= 535 && model <= 542) || (model >= 1649 && model <= 1650) || (model >= 1657 && model <= 1658) || (model >= 1667 && model <= 1668) || (model >= 1984 && model <= 1985) || model == 2015 || model == 2018 || (model >= 1888 && model <= 1889) || (model >= 1994 && model <= 1995) || model == 2045 || (model >= 334 && model <= 349) || (model >= 380 && model <= 389) || (model >= 559 && model <= 566) || (model >= 864 && model <= 867) || (model >= 1988 && model <= 1989) || (model >= 2110 && model <= 2111) || (model >= 2094 && model <= 2095) || (model >= 1075 && model <= 1098) || (model >= 1147 && model <= 1170) || (model >= 1653 && model <= 1654) || (model >= 1661 && model <= 1662) || (model >= 1992 && model <= 1993) || (model >= 1892 && model <= 1893) || (model >= 1998 && model <= 1999) || (model >= 700 && model <= 715) || (model >= 732 && model <= 747) || model == 767 || (model >= 849 && model <= 856) || (model >= 1655 && model <= 1656) || (model >= 1663 && model <= 1664) || (model >= 1990 && model <= 1991) || (model >= 286 && model <= 301) || (model >= 350 && model <= 359) || (model >= 551 && model <= 558) || (model >= 1651 && model <= 1652) || (model >= 1659 && model <= 1660) || (model >= 1665 && model <= 1666) || (model >= 1982 && model <= 1983) || (model >= 2096 && model <= 2097) || (model >= 318 && model <= 333) || (model >= 370 && model <= 379) || (model >= 543 && model <= 550) || model == 859 || (model >= 1986 && model <= 1987) || (model >= 1890 && model <= 1891) || (model >= 1996 && model <= 1997)) return 2;

            // Default Alb
            return 0;
        }

        public static bool IsMaskModel(int model)
        {
            if (model >= 4653 && model <= 4688) return true;
            if (model >= 4779 && model <= 4794) return true;
            if (model >= 4809 && model <= 4820) return true;
            if (model >= 4829 && model <= 4834) return true;
            if (model >= 4837 && model <= 4842) return true;
            if (model >= 4600 && model <= 4602) return true; // Eyes VFX
            return false;
        }

        public static int GetMaskModel(string typeStr, int npcModelId)
        {
            int body = GetNPCBodyCategory(npcModelId); // 0=Alb, 1=Mid, 2=Hib, 3=Mino
            
            typeStr = typeStr.ToLower();
            if (typeStr == "random" || typeStr == "none")
            {
                string[] types = { "eye", "horned", "slitted", "skull", "plague", "beast", "devour", "satyr", "death", "ornatedplague", "ornatedbeast", "ornatedsatyr", "ornatedskull", "yule", "demon", "pumpkin", "angel", "midona", "tentacled", "eyesvfx" };
                typeStr = types[Util.Random(types.Length - 1)];
            }

            switch (typeStr)
            {
                case "eye": return new[] {4653,4654,4655,4656}[body];
                case "horned": return new[] {4657,4658,4659,4660}[body];
                case "slitted": return new[] {4661,4662,4663,4664}[body];
                case "skull": return new[] {4665,4666,4667,4668}[body];
                case "plague": return new[] {4669,4670,4671,4672}[body];
                case "beast": return new[] {4673,4674,4675,4676}[body];
                case "devour": return new[] {4677,4678,4679,4680}[body];
                case "satyr": return new[] {4681,4682,4683,4684}[body];
                case "death": return new[] {4685,4686,4687,4688}[body];
                case "ornatedplague": return new[] {4779,4780,4781,4782}[body];
                case "ornatedbeast": return new[] {4783,4784,4785,4786}[body];
                case "ornatedsatyr": return new[] {4787,4788,4789,4790}[body];
                case "ornatedskull": return new[] {4791,4792,4793,4794}[body];
                case "yule": return new[] {4813,4814,4815,4813}[body]; // Mino uses Alb
                case "demon": return new[] {4829,4830,4831,4829}[body];
                case "pumpkin": return new[] {4809,4810,4811,4812}[body];
                case "angel": return new[] {4833,4832,4834,4833}[body];
                case "midona": return new[] {4837,4838,4839,4840}[body];
                case "tentacled": return new[] {4818,4819,4820,4818}[body];
                case "eyesvfx": return new[] {4600,4601,4602,4600}[body];
                case "headless": return 4816;
                case "krampus": return 4817;
            }
            return 4653;
        }

        #endregion
 
        #region Dictionaries & Initialization
        private static readonly Dictionary<string, PatternType> TemplateIdToType = new Dictionary<string, PatternType>(StringComparer.OrdinalIgnoreCase)
        {
            { "Possessed_Armor_pattern", PatternType.Possessed },
            { "Good_Armor_pattern", PatternType.Good },
            { "Corrupt_Armor_pattern", PatternType.Corrupt },
            { "Minotaur_Armor_pattern", PatternType.Minotaur },
            { "Oceanus_Armor_pattern", PatternType.Oceanus },
            { "Stygia_Armor_pattern", PatternType.Stygia },
            { "Volcanus_Armor_pattern", PatternType.Volcanus },
            { "Aerus_Armor_pattern", PatternType.Aerus }
        };

        private static readonly Dictionary<eCharacterClass, int[]> Epic1H = new Dictionary<eCharacterClass, int[]>();
        private static readonly Dictionary<eCharacterClass, int[]> Epic2H = new Dictionary<eCharacterClass, int[]>();
        private static readonly Dictionary<eCharacterClass, int[]> EpicDist = new Dictionary<eCharacterClass, int[]>();
        private static readonly Dictionary<eCharacterClass, int[]> DF1H = new Dictionary<eCharacterClass, int[]>();
        private static readonly Dictionary<eCharacterClass, int[]> DF2H = new Dictionary<eCharacterClass, int[]>();
        private static readonly Dictionary<eCharacterClass, int[]> DFDist = new Dictionary<eCharacterClass, int[]>();
        private static readonly Dictionary<eCharacterClass, int> EpicCloaks = new Dictionary<eCharacterClass, int>();

        static ItemModelManager()
        {
            foreach (PatternType pt in Enum.GetValues(typeof(PatternType)))
                TemplateIdToType[pt.ToString() + "_Armor_pattern"] = pt;

            // --- WEAPONS ---
            // 2H Staff/Scythe Classes
            Epic2H[eCharacterClass.Animist] = new[] { 3229 }; DF2H[eCharacterClass.Animist] = new[] { 4340 };
            Epic2H[eCharacterClass.Bainshee] = new[] { 3230 }; DF2H[eCharacterClass.Bainshee] = new[] { 4341 };
            Epic2H[eCharacterClass.Cabalist] = new[] { 3264 }; DF2H[eCharacterClass.Cabalist] = new[] { 4375 };
            Epic2H[eCharacterClass.Eldritch] = new[] { 3226 }; DF2H[eCharacterClass.Eldritch] = new[] { 4337 };
            Epic2H[eCharacterClass.Enchanter] = new[] { 3227 }; DF2H[eCharacterClass.Enchanter] = new[] { 4338 };
            Epic2H[eCharacterClass.Mentalist] = new[] { 3228 }; DF2H[eCharacterClass.Mentalist] = new[] { 4339 };
            Epic2H[eCharacterClass.Necromancer] = new[] { 3268 }; DF2H[eCharacterClass.Necromancer] = new[] { 4379 };
            Epic2H[eCharacterClass.Runemaster] = new[] { 3309 }; DF2H[eCharacterClass.Runemaster] = new[] { 4405 };
            Epic2H[eCharacterClass.Sorcerer] = new[] { 3265 }; DF2H[eCharacterClass.Sorcerer] = new[] { 4376 };
            Epic2H[eCharacterClass.Spiritmaster] = new[] { 3310 }; DF2H[eCharacterClass.Spiritmaster] = new[] { 4406 };
            Epic2H[eCharacterClass.Theurgist] = new[] { 3266 }; DF2H[eCharacterClass.Theurgist] = new[] { 4377 };
            Epic2H[eCharacterClass.Valewalker] = new[] { 3231 }; DF2H[eCharacterClass.Valewalker] = new[] { 4342 };
            Epic2H[eCharacterClass.Warlock] = new[] { 3312 }; DF2H[eCharacterClass.Warlock] = new[] { 4408 };
            Epic2H[eCharacterClass.Wizard] = new[] { 3267 }; DF2H[eCharacterClass.Wizard] = new[] { 4378 };
            Epic2H[eCharacterClass.Bonedancer] = new[] { 3311 }; DF2H[eCharacterClass.Bonedancer] = new[] { 4407 };
            Epic2H[eCharacterClass.Occultist] = new[] { 3312 }; DF2H[eCharacterClass.Occultist] = new[] { 4408 };

            // 1H ONLY (Right Hand)
            Epic1H[eCharacterClass.Bard] = new[] { 3235, 3236, 3237, 3238, 3239, 3240 }; DF1H[eCharacterClass.Bard] = new[] { 4346, 4347, 4348, 4349, 4350, 4351 };
            Epic1H[eCharacterClass.Cleric] = new[] { 3282 }; DF1H[eCharacterClass.Cleric] = new[] { 4393 };
            Epic1H[eCharacterClass.Druid] = new[] { 3247, 3248 }; DF1H[eCharacterClass.Druid] = new[] { 4358, 4359 };
            Epic1H[eCharacterClass.Mercenary] = new[] { 3283, 3284, 3285 }; DF1H[eCharacterClass.Mercenary] = new[] { 4394, 4395, 4396 };
            Epic1H[eCharacterClass.Minstrel] = new[] { 3276, 3277, 3278, 3279, 3280, 3281 }; DF1H[eCharacterClass.Minstrel] = new[] { 4387, 4388, 4389, 4390, 4391, 4392 };
            Epic1H[eCharacterClass.Reaver] = new[] { 3289, 3290, 3291, 3292, 3293 }; DF1H[eCharacterClass.Reaver] = new[] { 4400, 4401, 4402, 4403, 4404 };
            Epic1H[eCharacterClass.Warden] = new[] { 3249, 3250 }; DF1H[eCharacterClass.Warden] = new[] { 4360, 4361 };
            Epic1H[eCharacterClass.Heretic] = new[] { 3286, 3287, 3288 }; DF1H[eCharacterClass.Heretic] = new[] { 4397, 4398, 4399 };

            // 1H L+R ONLY
            Epic1H[eCharacterClass.Infiltrator] = new[] { 3269, 3270 }; DF1H[eCharacterClass.Infiltrator] = new[] { 4380, 4381 };
            Epic1H[eCharacterClass.Blademaster] = new[] { 3244, 3245, 3246 }; DF1H[eCharacterClass.Blademaster] = new[] { 4355, 4356, 4357 };
            Epic1H[eCharacterClass.Nightshade] = new[] { 3233, 3234 }; DF1H[eCharacterClass.Nightshade] = new[] { 4344, 4345 };
            Epic1H[eCharacterClass.Ranger] = new[] { 3241, 3242 }; DF1H[eCharacterClass.Ranger] = new[] { 4352, 4353 };
            Epic1H[eCharacterClass.Scout] = new[] { 3273, 3274 }; DF1H[eCharacterClass.Scout] = new[] { 4384, 4385 };
            Epic1H[eCharacterClass.Vampiir] = new[] { 3232 }; DF1H[eCharacterClass.Vampiir] = new[] { 4343 };

            // 50% 1H Right / 50% 2H
            Epic1H[eCharacterClass.Armsman] = new[] { 3294, 3295, 3296 }; DF1H[eCharacterClass.Armsman] = new[] { 4320, 4321, 4322 };
            Epic2H[eCharacterClass.Armsman] = new[] { 3297, 3298, 3299, 3300, 3301, 3302 }; DF2H[eCharacterClass.Armsman] = new[] { 4323, 4324, 4325, 4326, 4327, 4328 };

            Epic1H[eCharacterClass.Champion] = new[] { 3251, 3252, 3253 }; DF1H[eCharacterClass.Champion] = new[] { 4362, 4363, 4364 };
            Epic2H[eCharacterClass.Champion] = new[] { 3254, 3255 }; DF2H[eCharacterClass.Champion] = new[] { 4365, 4366 };

            Epic1H[eCharacterClass.Friar] = new[] { 3272 }; DF1H[eCharacterClass.Friar] = new[] { 4383 };
            Epic2H[eCharacterClass.Friar] = new[] { 3271 }; DF2H[eCharacterClass.Friar] = new[] { 4382 };

            Epic1H[eCharacterClass.Healer] = new[] { 3335 }; DF1H[eCharacterClass.Healer] = new[] { 4431 };
            Epic2H[eCharacterClass.Healer] = new[] { 3336 }; DF2H[eCharacterClass.Healer] = new[] { 4432 };

            Epic1H[eCharacterClass.Hero] = new[] { 3256, 3257, 3258 }; DF1H[eCharacterClass.Hero] = new[] { 4367, 4368, 4369 };
            Epic2H[eCharacterClass.Hero] = new[] { 3259, 3260, 3261, 3262, 3263 }; DF2H[eCharacterClass.Hero] = new[] { 4370, 4371, 4372, 4373, 4374 };

            Epic1H[eCharacterClass.Paladin] = new[] { 3303, 3304, 3305 }; DF1H[eCharacterClass.Paladin] = new[] { 4331, 4332, 4333 };
            Epic2H[eCharacterClass.Paladin] = new[] { 3306, 3307, 3308 }; DF2H[eCharacterClass.Paladin] = new[] { 4334, 4335, 4336 };

            Epic1H[eCharacterClass.Shaman] = new[] { 3337 }; DF1H[eCharacterClass.Shaman] = new[] { 4433 };
            Epic2H[eCharacterClass.Shaman] = new[] { 3338 }; DF2H[eCharacterClass.Shaman] = new[] { 4434 };

            Epic1H[eCharacterClass.Skald] = new[] { 3339, 3341, 3343 }; DF1H[eCharacterClass.Skald] = new[] { 4435, 4437, 4439 };
            Epic2H[eCharacterClass.Skald] = new[] { 3340, 3342, 3344 }; DF2H[eCharacterClass.Skald] = new[] { 4436, 4438, 4440 };

            Epic1H[eCharacterClass.Thane] = new[] { 3345, 3347, 3349 }; DF1H[eCharacterClass.Thane] = new[] { 4441, 4443, 4445 };
            Epic2H[eCharacterClass.Thane] = new[] { 3346, 3348, 3350 }; DF2H[eCharacterClass.Thane] = new[] { 4442, 4444, 4446 };

            Epic1H[eCharacterClass.Valkyrie] = new[] { 3357 }; DF1H[eCharacterClass.Valkyrie] = new[] { 4453 };
            Epic2H[eCharacterClass.Valkyrie] = new[] { 3358, 3362, 3363 }; DF2H[eCharacterClass.Valkyrie] = new[] { 4454, 4455, 4456 };

            Epic1H[eCharacterClass.Warrior] = new[] { 3351, 3353, 3355 }; DF1H[eCharacterClass.Warrior] = new[] { 4447, 4449, 4451 };
            Epic2H[eCharacterClass.Warrior] = new[] { 3352, 3354, 3356 }; DF2H[eCharacterClass.Warrior] = new[] { 4448, 4450, 4452 };

            // 50% 1H L+R / 50% 2H
            Epic1H[eCharacterClass.Berserker] = new[] { 3321, 3323, 3325 }; DF1H[eCharacterClass.Berserker] = new[] { 4417, 4419, 4421 };
            Epic2H[eCharacterClass.Berserker] = new[] { 3322, 3324, 3326 }; DF2H[eCharacterClass.Berserker] = new[] { 4418, 4420, 4422 };

            Epic1H[eCharacterClass.Hunter] = new[] { 3317 }; DF1H[eCharacterClass.Hunter] = new[] { 4413 };
            Epic2H[eCharacterClass.Hunter] = new[] { 3318, 3319, 3320 }; DF2H[eCharacterClass.Hunter] = new[] { 4414, 4415, 4416 };

            Epic1H[eCharacterClass.MaulerAlb] = new[] { 3566 }; DF1H[eCharacterClass.MaulerAlb] = new[] { 4461 };
            Epic2H[eCharacterClass.MaulerAlb] = new[] { 3565 }; DF2H[eCharacterClass.MaulerAlb] = new[] { 4460 };

            Epic1H[eCharacterClass.MaulerMid] = new[] { 3568 }; DF1H[eCharacterClass.MaulerMid] = new[] { 4463 };
            Epic2H[eCharacterClass.MaulerMid] = new[] { 3567 }; DF2H[eCharacterClass.MaulerMid] = new[] { 4462 };

            Epic1H[eCharacterClass.MaulerHib] = new[] { 3550 }; DF1H[eCharacterClass.MaulerHib] = new[] { 4459 };
            Epic2H[eCharacterClass.MaulerHib] = new[] { 3548 }; DF2H[eCharacterClass.MaulerHib] = new[] { 4458 };

            Epic1H[eCharacterClass.Savage] = new[] { 3327, 3329, 3331, 3333, 3334 }; DF1H[eCharacterClass.Savage] = new[] { 4423, 4425, 4427, 4429, 4430 };
            Epic2H[eCharacterClass.Savage] = new[] { 3328, 3330, 3332 }; DF2H[eCharacterClass.Savage] = new[] { 4424, 4426, 4428 };

            Epic1H[eCharacterClass.Shadowblade] = new[] { 3313, 3315 }; DF1H[eCharacterClass.Shadowblade] = new[] { 4409, 4411 };
            Epic2H[eCharacterClass.Shadowblade] = new[] { 3314, 3316 }; DF2H[eCharacterClass.Shadowblade] = new[] { 4410, 4412 };

            // Distance Weapons
            EpicDist[eCharacterClass.Hunter] = new[] { 3365 }; DFDist[eCharacterClass.Hunter] = new[] { 4457 };
            EpicDist[eCharacterClass.Ranger] = new[] { 3243 }; DFDist[eCharacterClass.Ranger] = new[] { 4354 };
            EpicDist[eCharacterClass.Scout] = new[] { 3275 }; DFDist[eCharacterClass.Scout] = new[] { 4386 };

            // --- CLOAKS ---
            EpicCloaks[eCharacterClass.Animist] = 4605; EpicCloaks[eCharacterClass.Armsman] = 4606;
            EpicCloaks[eCharacterClass.Bainshee] = 4607; EpicCloaks[eCharacterClass.Bard] = 4608;
            EpicCloaks[eCharacterClass.Berserker] = 4609; EpicCloaks[eCharacterClass.Blademaster] = 4610;
            EpicCloaks[eCharacterClass.Bonedancer] = 4611; EpicCloaks[eCharacterClass.Cabalist] = 4612;
            EpicCloaks[eCharacterClass.Champion] = 4613; EpicCloaks[eCharacterClass.Cleric] = 4614;
            EpicCloaks[eCharacterClass.Druid] = 4615; EpicCloaks[eCharacterClass.Eldritch] = 4616;
            EpicCloaks[eCharacterClass.Enchanter] = 4617; EpicCloaks[eCharacterClass.Friar] = 4618;
            EpicCloaks[eCharacterClass.Healer] = 4619; EpicCloaks[eCharacterClass.Heretic] = 4620;
            EpicCloaks[eCharacterClass.Hero] = 4621; EpicCloaks[eCharacterClass.Hunter] = 4622;
            EpicCloaks[eCharacterClass.Infiltrator] = 4623; EpicCloaks[eCharacterClass.MaulerAlb] = 4648;
            EpicCloaks[eCharacterClass.MaulerMid] = 4649; EpicCloaks[eCharacterClass.MaulerHib] = 4650;
            EpicCloaks[eCharacterClass.Mentalist] = 4624; EpicCloaks[eCharacterClass.Mercenary] = 4625;
            EpicCloaks[eCharacterClass.Minstrel] = 4626; EpicCloaks[eCharacterClass.Necromancer] = 4627;
            EpicCloaks[eCharacterClass.Nightshade] = 4628; EpicCloaks[eCharacterClass.Paladin] = 4629;
            EpicCloaks[eCharacterClass.Ranger] = 4630; EpicCloaks[eCharacterClass.Reaver] = 4631;
            EpicCloaks[eCharacterClass.Runemaster] = 4632; EpicCloaks[eCharacterClass.Savage] = 4633;
            EpicCloaks[eCharacterClass.Scout] = 4634; EpicCloaks[eCharacterClass.Shadowblade] = 4635;
            EpicCloaks[eCharacterClass.Shaman] = 4636; EpicCloaks[eCharacterClass.Skald] = 4637;
            EpicCloaks[eCharacterClass.Sorcerer] = 4638; EpicCloaks[eCharacterClass.Spiritmaster] = 4639;
            EpicCloaks[eCharacterClass.Thane] = 4644; EpicCloaks[eCharacterClass.Theurgist] = 4640;
            EpicCloaks[eCharacterClass.Valewalker] = 4641; EpicCloaks[eCharacterClass.Valkyrie] = 4645;
            EpicCloaks[eCharacterClass.Vampiir] = 4642; EpicCloaks[eCharacterClass.Warden] = 4643;
            EpicCloaks[eCharacterClass.Warlock] = 4646; EpicCloaks[eCharacterClass.Warrior] = 4647;
            EpicCloaks[eCharacterClass.Wizard] = 4604;
            EpicCloaks[eCharacterClass.Occultist] = 4646;
        }
        #endregion

        #region Helpers & Public APIs

        public static int GetEasterEggHelm(eRealm realm)
        {
            switch (realm)
            {
                case eRealm.Albion: return Util.Chance(33) ? 1284 : (Util.Chance(50) ? 1281 : 1287);
                case eRealm.Hibernia: return Util.Chance(33) ? 1282 : (Util.Chance(50) ? 1285 : 1288);
                case eRealm.Midgard: return Util.Chance(33) ? 1289 : (Util.Chance(50) ? 1283 : 1286);
                default: return 0;
            }
        }

        public static void EquipClassWeapons(GameNPC npc, GameNpcInventoryTemplate template, eCharacterClass cClass, string wTemplate, ushort color)
        {
            bool isDF = wTemplate.Equals("epicdf", StringComparison.OrdinalIgnoreCase);
            bool isClassic = wTemplate.Equals("classic", StringComparison.OrdinalIgnoreCase);

            int w1H_model = 0, w2H_model = 0, wDist_model = 0, wLeft_model = 0;
            int level = npc.Level > 0 ? npc.Level : 50;
            eRealm realm = npc.Realm == eRealm.None ? eRealm.Albion : npc.Realm;

            bool isDualWieldClass = cClass == eCharacterClass.Infiltrator || cClass == eCharacterClass.Blademaster ||
                                    cClass == eCharacterClass.Nightshade || cClass == eCharacterClass.Ranger ||
                                    cClass == eCharacterClass.Scout || cClass == eCharacterClass.Vampiir ||
                                    cClass == eCharacterClass.Berserker || cClass == eCharacterClass.Hunter ||
                                    cClass == eCharacterClass.MaulerAlb || cClass == eCharacterClass.MaulerMid ||
                                    cClass == eCharacterClass.MaulerHib || cClass == eCharacterClass.Savage ||
                                    cClass == eCharacterClass.Shadowblade;

            if (isClassic)
            {
                List<eObjectType> classTypes = GetClassWeaponTypes(cClass);

                var oneHandTypes = classTypes.Where(t => t == eObjectType.SlashingWeapon || t == eObjectType.CrushingWeapon || t == eObjectType.ThrustWeapon || t == eObjectType.Sword || t == eObjectType.Axe || t == eObjectType.Hammer || t == eObjectType.Blades || t == eObjectType.Blunt || t == eObjectType.Piercing || t == eObjectType.Flexible || t == eObjectType.HandToHand || t == eObjectType.FistWraps).ToList();

                var twoHandTypes = classTypes.Where(t => t == eObjectType.TwoHandedWeapon || t == eObjectType.PolearmWeapon || t == eObjectType.Staff || t == eObjectType.Spear || t == eObjectType.CelticSpear || t == eObjectType.LargeWeapons || t == eObjectType.Scythe || t == eObjectType.MaulerStaff).ToList();

                var distTypes = classTypes.Where(t => t == eObjectType.Longbow || t == eObjectType.Crossbow || t == eObjectType.CompositeBow || t == eObjectType.RecurvedBow || t == eObjectType.Fired || t == eObjectType.Instrument).ToList();

                var leftTypes = classTypes.Where(t => t == eObjectType.Shield || t == eObjectType.LeftAxe).ToList();

                if (oneHandTypes.Count > 0)
                {
                    eObjectType selectedType = oneHandTypes[Util.Random(0, oneHandTypes.Count - 1)];
                    eDamageType dmg = GenerateDamageType(selectedType, cClass);
                    GetWeaponData(selectedType, realm, level, 1, dmg, 35, cClass, out w1H_model, out _, out _, out _, out _);
                }
                if (twoHandTypes.Count > 0)
                {
                    eObjectType selectedType = twoHandTypes[Util.Random(0, twoHandTypes.Count - 1)];
                    eDamageType dmg = GenerateDamageType(selectedType, cClass);
                    GetWeaponData(selectedType, realm, level, 1, dmg, 35, cClass, out w2H_model, out _, out _, out _, out _);
                }
                if (distTypes.Count > 0)
                {
                    eObjectType selectedType = distTypes[Util.Random(0, distTypes.Count - 1)];
                    eDamageType dmg = GenerateDamageType(selectedType, cClass);
                    GetWeaponData(selectedType, realm, level, 1, dmg, 35, cClass, out wDist_model, out _, out _, out _, out _);
                }
                if (leftTypes.Count > 0)
                {
                    eObjectType selectedType = leftTypes[Util.Random(0, leftTypes.Count - 1)];
                    eDamageType dmg = GenerateDamageType(selectedType, cClass);
                    GetWeaponData(selectedType, realm, level, 2, dmg, 35, cClass, out wLeft_model, out _, out _, out _, out _);
                }
                else if (isDualWieldClass && oneHandTypes.Count > 0) // give dual wielding capability
                {
                    eObjectType selectedType = oneHandTypes[Util.Random(0, oneHandTypes.Count - 1)];
                    eDamageType dmg = GenerateDamageType(selectedType, cClass);
                    GetWeaponData(selectedType, realm, level, 2, dmg, 35, cClass, out wLeft_model, out _, out _, out _, out _);
                }
            }
            else // Epic or EpicDF templates
            {
                int[] arr1H = isDF ? (DF1H.ContainsKey(cClass) ? DF1H[cClass] : null) : (Epic1H.ContainsKey(cClass) ? Epic1H[cClass] : null);
                int[] arr2H = isDF ? (DF2H.ContainsKey(cClass) ? DF2H[cClass] : null) : (Epic2H.ContainsKey(cClass) ? Epic2H[cClass] : null);
                int[] arrDist = isDF ? (DFDist.ContainsKey(cClass) ? DFDist[cClass] : null) : (EpicDist.ContainsKey(cClass) ? EpicDist[cClass] : null);

                if (arr1H != null && arr1H.Length > 0) w1H_model = arr1H[Util.Random(arr1H.Length - 1)];
                if (arr2H != null && arr2H.Length > 0) w2H_model = arr2H[Util.Random(arr2H.Length - 1)];
                if (arrDist != null && arrDist.Length > 0) wDist_model = arrDist[Util.Random(arrDist.Length - 1)];

                if (isDualWieldClass && arr1H != null && arr1H.Length > 0 && w1H_model > 0) wLeft_model = arr1H[Util.Random(arr1H.Length - 1)];
            }

            GameLiving.eActiveWeaponSlot visibleSlot = GameLiving.eActiveWeaponSlot.Standard;

            if (w1H_model > 0 && w2H_model > 0)
            {
                if (Util.Chance(50))
                {
                    template.AddNPCEquipment(eInventorySlot.TwoHandWeapon, (ushort)w2H_model, color, 0);
                    visibleSlot = GameLiving.eActiveWeaponSlot.TwoHanded;
                }
                else
                {
                    template.AddNPCEquipment(eInventorySlot.RightHandWeapon, (ushort)w1H_model, color, 0);
                    if (wLeft_model > 0) template.AddNPCEquipment(eInventorySlot.LeftHandWeapon, (ushort)wLeft_model, color, 0);
                    visibleSlot = GameLiving.eActiveWeaponSlot.Standard;
                }
            }
            else if (w2H_model > 0)
            {
                template.AddNPCEquipment(eInventorySlot.TwoHandWeapon, (ushort)w2H_model, color, 0);
                visibleSlot = GameLiving.eActiveWeaponSlot.TwoHanded;
            }
            else if (w1H_model > 0)
            {
                template.AddNPCEquipment(eInventorySlot.RightHandWeapon, (ushort)w1H_model, color, 0);
                if (wLeft_model > 0) template.AddNPCEquipment(eInventorySlot.LeftHandWeapon, (ushort)wLeft_model, color, 0);
                visibleSlot = GameLiving.eActiveWeaponSlot.Standard;
            }

            if (wDist_model > 0)
            {
                template.AddNPCEquipment(eInventorySlot.DistanceWeapon, (ushort)wDist_model, color, 0);
            }

            npc.Inventory = template;
            npc.SwitchWeapon(visibleSlot);
            npc.VisibleWeaponsDb = npc.VisibleActiveWeaponSlots;
        }

        public static int GetCloakModel(string category, eCharacterClass cClass = eCharacterClass.Unknown)
        {
            switch (category.ToLower())
            {
                case "toa": return Util.Random(1720, 1727);
                case "guard": int[] g = { 669, 677, 678 }; return g[Util.Random(0, g.Length - 1)];
                case "realm": int[] r = { 3801, 3802, 3803 }; return r[Util.Random(0, r.Length - 1)];
                case "otherworldly": int[] o = { 4557, 4558, 4559 }; return o[Util.Random(0, o.Length - 1)];
                case "special": int[] s = { 3752, 4115, 4822, 4823 }; return s[Util.Random(0, s.Length - 1)];
                case "class":
                    if (cClass != eCharacterClass.Unknown && EpicCloaks.TryGetValue(cClass, out int cloakModel)) return cloakModel;
                    return Util.Random(4604, 4650);
                case "regular":
                default:
                    int[] reg = { 57, 91, 92, 96, 144, 326, 443, 467, 557, 558, 559, 560 };
                    return reg[Util.Random(0, reg.Length - 1)];
            }
        }

        public static int GetClassEpicArmor(eCharacterClass cClass, eInventorySlot slot)
        {
            int s = (int)slot;
            switch (cClass)
            {
                // Albion Fighters
                case eCharacterClass.Armsman: return s == 21 ? 1290 : s == 25 ? 688 : s == 27 ? 689 : s == 28 ? 690 : s == 22 ? 691 : s == 23 ? 692 : 0;
                case eCharacterClass.Paladin: return s == 21 ? 1290 : s == 25 ? 693 : s == 27 ? 694 : s == 28 ? 695 : s == 22 ? 696 : s == 23 ? 697 : 0;
                case eCharacterClass.Cleric: return s == 21 ? 1290 : s == 25 ? 713 : s == 27 ? 714 : s == 28 ? 715 : s == 22 ? 716 : s == 23 ? 717 : 0;
                case eCharacterClass.Mercenary: return s == 21 ? 1290 : s == 25 ? 718 : s == 27 ? 719 : s == 28 ? 720 : s == 22 ? 721 : s == 23 ? 722 : 0;
                case eCharacterClass.Minstrel: return s == 21 ? 1290 : s == 25 ? 3380 : s == 27 ? 3381 : s == 28 ? 3382 : s == 22 ? 3383 : s == 23 ? 3384 : 0;
                case eCharacterClass.Scout: return s == 21 ? 1281 : s == 25 ? 728 : s == 27 ? 729 : s == 28 ? 730 : s == 22 ? 732 : s == 23 ? 731 : 0;
                case eCharacterClass.Infiltrator: return s == 21 ? 1290 : s == 25 ? 792 : s == 27 ? 793 : s == 28 ? 794 : s == 22 ? 795 : s == 23 ? 796 : 0;
                case eCharacterClass.Reaver: return s == 21 ? 1290 : s == 25 ? 1267 : s == 27 ? 1268 : s == 28 ? 1269 : s == 22 ? 1271 : s == 23 ? 1270 : 0;
                case eCharacterClass.MaulerAlb: return s == 21 ? 1290 : s == 25 ? 3638 : s == 27 ? 3639 : s == 28 ? 3640 : s == 22 ? 3641 : s == 23 ? 3642 : 0;

                // Midgard Fighters
                case eCharacterClass.Healer: return s == 21 ? 1291 : s == 25 ? 698 : s == 27 ? 699 : s == 28 ? 700 : s == 22 ? 701 : s == 23 ? 702 : 0;
                case eCharacterClass.Berserker: return s == 21 ? 1289 : s == 25 ? 3375 : s == 27 ? 3376 : s == 28 ? 3377 : s == 22 ? 3378 : s == 23 ? 3379 : 0;
                case eCharacterClass.Hunter: return s == 21 ? 1289 : s == 25 ? 3385 : s == 27 ? 3386 : s == 28 ? 3387 : s == 22 ? 3388 : s == 23 ? 3389 : 0;
                case eCharacterClass.Shadowblade: return s == 21 ? 1291 : s == 25 ? 761 : s == 27 ? 762 : s == 28 ? 763 : s == 22 ? 764 : s == 23 ? 765 : 0;
                case eCharacterClass.Shaman: return s == 21 ? 1291 : s == 25 ? 766 : s == 27 ? 767 : s == 28 ? 768 : s == 22 ? 769 : s == 23 ? 770 : 0;
                case eCharacterClass.Skald: return s == 21 ? 1291 : s == 25 ? 771 : s == 27 ? 772 : s == 28 ? 773 : s == 22 ? 774 : s == 23 ? 775 : 0;
                case eCharacterClass.Warrior: return s == 21 ? 1291 : s == 25 ? 776 : s == 27 ? 777 : s == 28 ? 778 : s == 22 ? 779 : s == 23 ? 780 : 0;
                case eCharacterClass.Thane: return s == 21 ? 1291 : s == 25 ? 3370 : s == 27 ? 3371 : s == 28 ? 3372 : s == 22 ? 3373 : s == 23 ? 3374 : 0;
                case eCharacterClass.Savage: return s == 21 ? 1289 : s == 25 ? 1192 : s == 27 ? 1193 : s == 28 ? 1194 : s == 22 ? 1195 : s == 23 ? 1196 : 0;
                case eCharacterClass.Valkyrie: return s == 21 ? 2951 : s == 25 ? 2928 : s == 27 ? 2929 : s == 28 ? 2930 : s == 22 ? 2931 : s == 23 ? 2932 : 0;
                case eCharacterClass.MaulerMid: return s == 21 ? 1291 : s == 25 ? 3570 : s == 27 ? 3571 : s == 28 ? 3572 : s == 22 ? 3573 : s == 23 ? 3574 : 0;

                // Hibernia Fighters
                case eCharacterClass.Hero: return s == 21 ? 1292 : s == 25 ? 708 : s == 27 ? 709 : s == 28 ? 710 : s == 22 ? 711 : s == 23 ? 712 : 0;
                case eCharacterClass.Bard: return s == 21 ? 1292 : s == 25 ? 734 : s == 27 ? 735 : s == 28 ? 736 : s == 22 ? 737 : s == 23 ? 738 : 0;
                case eCharacterClass.Druid: return s == 21 ? 1292 : s == 25 ? 739 : s == 27 ? 740 : s == 28 ? 741 : s == 22 ? 742 : s == 23 ? 743 : 0;
                case eCharacterClass.Nightshade: return s == 21 ? 1292 : s == 25 ? 746 : s == 27 ? 747 : s == 28 ? 748 : s == 22 ? 749 : s == 23 ? 750 : 0;
                case eCharacterClass.Blademaster: return s == 21 ? 1292 : s == 25 ? 3390 : s == 27 ? 3391 : s == 28 ? 3392 : s == 22 ? 3393 : s == 23 ? 3394 : 0;
                case eCharacterClass.Warden: return s == 21 ? 1292 : s == 25 ? 805 : s == 27 ? 806 : s == 28 ? 807 : s == 22 ? 808 : s == 23 ? 809 : 0;
                case eCharacterClass.Champion: return s == 21 ? 1292 : s == 25 ? 810 : s == 27 ? 811 : s == 28 ? 812 : s == 22 ? 813 : s == 23 ? 814 : 0;
                case eCharacterClass.Ranger: return s == 21 ? 1282 : s == 25 ? 815 : s == 27 ? 816 : s == 28 ? 817 : s == 22 ? 818 : s == 23 ? 0 : 0;
                case eCharacterClass.Vampiir: return s == 21 ? 1292 : s == 25 ? 2923 : s == 27 ? 2924 : s == 28 ? 2925 : s == 22 ? 2926 : s == 23 ? 2927 : 0;
                case eCharacterClass.MaulerHib: return s == 21 ? 1292 : s == 25 ? 3560 : s == 27 ? 3561 : s == 28 ? 3562 : s == 22 ? 3563 : s == 23 ? 3564 : 0;

                // Albion Mages
                case eCharacterClass.Cabalist: return s == 21 ? 1278 : s == 25 ? 682 : 0;
                case eCharacterClass.Friar: return s == 21 ? 1278 : s == 25 ? 687 : 0;
                case eCharacterClass.Theurgist: return s == 21 ? 1278 : s == 25 ? 733 : 0;
                case eCharacterClass.Wizard: return s == 21 ? 1278 : s == 25 ? 798 : 0;
                case eCharacterClass.Necromancer: return s == 21 ? 1278 : s == 25 ? 1266 : 0;
                case eCharacterClass.Heretic: return s == 21 ? 1278 : s == 25 ? 2921 : 0;
                case eCharacterClass.Sorcerer: return s == 21 ? 1278 : s == 25 ? 3369 : s == 22 ? 3367 : s == 23 ? 3366 : 0;
                case eCharacterClass.Occultist: return s == 21 ? 1278 : s == 25 ? 3369 : s == 22 ? 3367 : s == 23 ? 3366 : 0;

                // Midgard Mages
                case eCharacterClass.Runemaster: return s == 21 ? 1280 : s == 25 ? 703 : s == 27 ? 704 : s == 28 ? 705 : s == 22 ? 706 : s == 23 ? 707 : 0;
                case eCharacterClass.Spiritmaster: return s == 21 ? 1280 : s == 25 ? 799 : s == 27 ? 800 : s == 28 ? 801 : s == 22 ? 802 : s == 23 ? 803 : 0;
                case eCharacterClass.Bonedancer: return s == 21 ? 1280 : s == 25 ? 1187 : s == 27 ? 1188 : s == 28 ? 1189 : s == 22 ? 1191 : s == 23 ? 1190 : 0;
                case eCharacterClass.Warlock: return s == 21 ? 1280 : s == 25 ? 2933 : s == 27 ? 2934 : s == 22 ? 2936 : s == 23 ? 2937 : 0;

                // Hibernia Mages
                case eCharacterClass.Eldritch: return s == 21 ? 1279 : s == 25 ? 744 : 0;
                case eCharacterClass.Mentalist: return s == 21 ? 1279 : s == 25 ? 745 : 0;
                case eCharacterClass.Enchanter: return s == 21 ? 1279 : s == 25 ? 781 : 0;
                case eCharacterClass.Valewalker: return s == 21 ? 1279 : s == 25 ? 1003 : 0;
                case eCharacterClass.Animist: return s == 21 ? 1279 : s == 25 ? 1186 : 0;
                case eCharacterClass.Bainshee: return s == 21 ? 1279 : s == 25 ? 2922 : s == 27 ? 2949 : s == 28 ? 2948 : s == 22 ? 2950 : s == 23 ? 2952 : 0;
            }
            return 0;
        }

        public static eObjectType GetDefaultArmorMaterialForClass(eCharacterClass cClass, int level)
        {
            switch (cClass)
            {
                case eCharacterClass.Armsman: case eCharacterClass.Paladin: case eCharacterClass.Reaver: case eCharacterClass.Cleric:
                    return level >= 20 ? eObjectType.Plate : (level >= 10 ? eObjectType.Chain : eObjectType.Studded);
                case eCharacterClass.Mercenary: case eCharacterClass.Minstrel: case eCharacterClass.Scout:
                    return level >= 20 ? eObjectType.Chain : eObjectType.Studded;
                case eCharacterClass.Infiltrator: case eCharacterClass.Friar: case eCharacterClass.MaulerAlb: case eCharacterClass.MaulerMid:
                case eCharacterClass.MaulerHib: case eCharacterClass.Nightshade: case eCharacterClass.Ranger: case eCharacterClass.MidgardRogue:
                case eCharacterClass.AlbionRogue: case eCharacterClass.Stalker:
                    return eObjectType.Leather;
                case eCharacterClass.Thane: case eCharacterClass.Warrior: case eCharacterClass.Skald: case eCharacterClass.Valkyrie:
                case eCharacterClass.Healer: case eCharacterClass.Shaman:
                    return level >= 20 ? eObjectType.Chain : eObjectType.Studded;
                case eCharacterClass.Berserker: case eCharacterClass.Savage: case eCharacterClass.Shadowblade: case eCharacterClass.Viking:
                    return eObjectType.Studded;
                case eCharacterClass.Champion: case eCharacterClass.Hero: case eCharacterClass.Warden:
                    return level >= 20 ? eObjectType.Scale : eObjectType.Reinforced;
                case eCharacterClass.Blademaster: case eCharacterClass.Bard:
                    return eObjectType.Reinforced;
                default:
                    return eObjectType.Cloth;
            }
        }

        public static int GetPatternModelForTargetItem(string patternString, InventoryItem item)
        {
            return GetPatternModel(patternString, item.Object_Type, item.Item_Type, item.Realm);
        }

        public static PatternType GetPatternType(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return PatternType.None;

            if (TemplateIdToType.TryGetValue(templateId, out PatternType pt))
                return pt;

            if (Enum.TryParse(templateId, true, out PatternType parsedType))
                return parsedType;

            return PatternType.None;
        }

        public static int GetPatternModelForTargetItem(PatternType patternType, InventoryItem item)
        {
            return GetPatternModelForTargetItem(patternType.ToString(), item);
        }

        public static int GetRandomWeaponModelForNgg(int slotIndex, eRealm realm)
        {
            int level = Util.Random(40, 50);
            try
            {
                if (slotIndex == 7 || slotIndex == 8) return GetBladeModelForLevel(level, realm);
                if (slotIndex == 9) return Get2HSwordForLevel(level, realm);
                if (slotIndex == 10) return GetBowModelForLevel(level, realm);
            }
            catch { return 0; }
            return 0;
        }

        private static int GetPatternModel(string patternString, int material, int slot, int realm)
        {
            if (realm == 0)
            {
                if (material == 36) realm = 1;      // Plate = Alb
                else if (material == 35) realm = 2; // Chain = Mid
                else if (material == 38 || material == 37) realm = 3; // Scale/Reinforced = Hib
                else if (material == 34) realm = 2; // Studded = Mid
                else realm = 1;
            }

            // Route Class Patterns (Only for Cloth, Leather, Studded, Reinforced)
            if (patternString.StartsWith("Class", StringComparison.OrdinalIgnoreCase))
            {
                if (material == 36 || material == 35 || material == 38) return -1; // No Chain/Plate/Scale
                
                if (Enum.TryParse(patternString.Substring(5), true, out eCharacterClass cClass))
                {
                    int cModel = GetClassEpicArmor(cClass, (eInventorySlot)slot);
                    if (cModel > 0) return cModel;
                }
                return -1;
            }

            if (Enum.TryParse(patternString, true, out PatternType pType))
            {
                switch (pType)
                {
                    case PatternType.Possessed: return GetPossessedModel(realm, material, slot);
                    case PatternType.Good: return GetGoodModel(realm, material, slot);
                    case PatternType.Corrupt: return GetCorruptModel(material, slot);
                    case PatternType.Minotaur: return GetMinotaurModel(material, slot);
                    case PatternType.Oceanus: return GetToaModel_Oceanus(realm, material, slot);
                    case PatternType.Stygia: return GetToaModel_Stygia(realm, material, slot);
                    case PatternType.Volcanus: return GetToaModel_Volcanus(realm, material, slot);
                    case PatternType.Aerus: return GetToaModel_Aerus(realm, material, slot);
                }
            }
            
            return -1;
        }

        public static void GetArmorData(eObjectType material, eInventorySlot slot, int level, eRealm realm, string pattern, int npcModelId, out int model, out string name, out bool canAddExtension, out int headeffect)
        {
            if (realm == eRealm.None) realm = (eRealm)Util.Random(1, 3);
            model = 0;
            headeffect = 0;
            name = material.ToString();
            canAddExtension = false;

            if (pattern.StartsWith("Class", StringComparison.OrdinalIgnoreCase) && (material == eObjectType.Cloth || material == eObjectType.Leather || material == eObjectType.Studded || material == eObjectType.Reinforced))
            {
                if (Enum.TryParse(pattern.Substring(5), true, out eCharacterClass cClass))
                {
                    int pModel = GetClassEpicArmor(cClass, slot);
                    if (pModel > 0)
                    {
                        model = pModel;
                        name = cClass.ToString() + " Armor";
                        return;
                    }
                }
            }

            if (!pattern.Equals("None", StringComparison.OrdinalIgnoreCase) && !pattern.StartsWith("Class", StringComparison.OrdinalIgnoreCase))
            {
                int pModel = GetPatternModel(pattern, (int)material, (int)slot, (int)realm);
                if (pModel > 0)
                {
                    model = pModel;
                    name = pattern.ToString() + " Armor";
                    return;
                }
            }

            switch (material)
            {
                case eObjectType.Cloth:
                    if (slot == eInventorySlot.HeadArmor) model = Util.Chance(30) ? (realm == eRealm.Albion ? 1278 : realm == eRealm.Midgard ? 1280 : 1279) : (realm == eRealm.Albion ? 822 : realm == eRealm.Midgard ? 825 : 826);
                    else if (slot == eInventorySlot.ArmsArmor) model = realm == eRealm.Albion ? 141 : realm == eRealm.Midgard ? 247 : 380;
                    else if (slot == eInventorySlot.LegsArmor) model = realm == eRealm.Albion ? 140 : realm == eRealm.Midgard ? 246 : 379;
                    else if (slot == eInventorySlot.HandsArmor) model = realm == eRealm.Albion ? 142 : realm == eRealm.Midgard ? 248 : 381;
                    else if (slot == eInventorySlot.FeetArmor) model = realm == eRealm.Albion ? 143 : realm == eRealm.Midgard ? 249 : 382;
                    else if (slot == eInventorySlot.TorsoArmor)
                    {
                        if (Util.Chance(60)) model = realm == eRealm.Albion ? 139 : realm == eRealm.Midgard ? 245 : 378;
                        else { name = "Cloth Robe"; model = Util.Chance(33) ? 58 : (Util.Chance(50) ? 65 : 66); }
                    }
                    canAddExtension = (slot != eInventorySlot.HeadArmor);
                    break;
                case eObjectType.Leather:
                    if (slot == eInventorySlot.TorsoArmor) model = GetLeatherTorsoForLevel(level, realm);
                    else if (slot == eInventorySlot.LegsArmor) model = GetLeatherPantsForLevel(level, realm);
                    else if (slot == eInventorySlot.ArmsArmor) model = GetLeatherSleevesForLevel(level, realm);
                    else if (slot == eInventorySlot.HandsArmor) model = GetLeatherHandsForLevel(level, realm);
                    else if (slot == eInventorySlot.FeetArmor) model = GetLeatherBootsForLevel(level, realm);
                    else if (slot == eInventorySlot.HeadArmor) model = GetLeatherHelmForLevel(level, realm);
                    canAddExtension = (slot != eInventorySlot.HeadArmor && slot != eInventorySlot.ArmsArmor && slot != eInventorySlot.LegsArmor);
                    break;
                case eObjectType.Studded:
                    if (slot == eInventorySlot.TorsoArmor) model = GetStuddedTorsoForLevel(level, realm);
                    else if (slot == eInventorySlot.LegsArmor) model = GetStuddedPantsForLevel(level, realm);
                    else if (slot == eInventorySlot.ArmsArmor) model = GetStuddedSleevesForLevel(level, realm);
                    else if (slot == eInventorySlot.HandsArmor) model = GetStuddedHandsForLevel(level, realm);
                    else if (slot == eInventorySlot.FeetArmor) model = GetStuddedBootsForLevel(level, realm);
                    else if (slot == eInventorySlot.HeadArmor) model = GetStuddedHelmForLevel(level, realm);
                    canAddExtension = (slot != eInventorySlot.HeadArmor && slot != eInventorySlot.ArmsArmor && slot != eInventorySlot.LegsArmor);
                    break;
                case eObjectType.Chain:
                    if (slot == eInventorySlot.TorsoArmor) model = GetChainTorsoForLevel(level, realm);
                    else if (slot == eInventorySlot.LegsArmor) model = GetChainPantsForLevel(level, realm);
                    else if (slot == eInventorySlot.ArmsArmor) model = GetChainSleevesForLevel(level, realm);
                    else if (slot == eInventorySlot.HandsArmor) model = GetChainHandsForLevel(level, realm);
                    else if (slot == eInventorySlot.FeetArmor) model = GetChainBootsForLevel(level, realm);
                    else if (slot == eInventorySlot.HeadArmor) model = GetChainHelmForLevel(level, realm);
                    canAddExtension = (slot != eInventorySlot.HeadArmor);
                    break;
                case eObjectType.Scale:
                    if (slot == eInventorySlot.TorsoArmor) model = GetScaleTorsoForLevel(level, realm);
                    else if (slot == eInventorySlot.LegsArmor) model = GetScalePantsForLevel(level, realm);
                    else if (slot == eInventorySlot.ArmsArmor) model = GetScaleSleevesForLevel(level, realm);
                    else if (slot == eInventorySlot.HandsArmor) model = GetScaleHandsForLevel(level, realm);
                    else if (slot == eInventorySlot.FeetArmor) model = GetScaleBootsForLevel(level, realm);
                    else if (slot == eInventorySlot.HeadArmor) model = GetScaleHelmForLevel(level, realm);
                    canAddExtension = (slot != eInventorySlot.HeadArmor);
                    break;
                case eObjectType.Reinforced:
                    if (slot == eInventorySlot.TorsoArmor) model = GetReinforcedTorsoForLevel(level, realm);
                    else if (slot == eInventorySlot.LegsArmor) model = GetReinforcedPantsForLevel(level, realm);
                    else if (slot == eInventorySlot.ArmsArmor) model = GetReinforcedSleevesForLevel(level, realm);
                    else if (slot == eInventorySlot.HandsArmor) model = GetReinforcedHandsForLevel(level, realm);
                    else if (slot == eInventorySlot.FeetArmor) model = GetReinforcedBootsForLevel(level, realm);
                    else if (slot == eInventorySlot.HeadArmor) model = GetReinforcedHelmForLevel(level, realm);
                    canAddExtension = (slot != eInventorySlot.HeadArmor);
                    break;
                case eObjectType.Plate:
                    if (slot == eInventorySlot.TorsoArmor) model = GetPlateTorsoForLevel(level, realm);
                    else if (slot == eInventorySlot.LegsArmor) model = GetPlatePantsForLevel(level, realm);
                    else if (slot == eInventorySlot.ArmsArmor) model = GetPlateSleevesForLevel(level, realm);
                    else if (slot == eInventorySlot.HandsArmor) model = GetPlateHandsForLevel(level, realm);
                    else if (slot == eInventorySlot.FeetArmor) model = GetPlateBootsForLevel(level, realm);
                    else if (slot == eInventorySlot.HeadArmor) {
                        model = GetPlateHelmForLevel(level, realm);
                        if (model == 93 || model == 95) name = "Plate Full Helm";
                    }
                    canAddExtension = (slot != eInventorySlot.HeadArmor);
                    break;
            }

            //each realm has a chance for special helmets during generation
            if (slot == eInventorySlot.HeadArmor && Util.Chance(2))
            {
                if (realm == eRealm.Albion) model = Util.Chance(33) ? 1284 : (Util.Chance(50) ? 1281 : 1287); //2% chance of tarboosh, robin hood hat, jester hat
                else if (realm == eRealm.Hibernia) model = Util.Chance(33) ? 1282 : (Util.Chance(50) ? 1285 : 1288); //2% chance of robin hood hat, leaf hat, stag helm
                else model = Util.Chance(33) ? 1289 : (Util.Chance(50) ? 1283 : 1286); //2% chance of wolf hat, fur cap, wing hat
            }
        }

        public static void GetWeaponData(eObjectType type, eRealm realm, int level, int hand, eDamageType dmg, int spdAbs, eCharacterClass charClass, out int model, out string name, out int newHand, out eInventorySlot slot, out int effect)
        {
            if (realm == eRealm.None) realm = (eRealm)Util.Random(1, 3);
            model = 0; name = "Weapon"; newHand = hand; slot = eInventorySlot.RightHandWeapon; effect = 0;

            switch (type)
            {
                case eObjectType.Axe:
                    if (hand == 1) { model = Get2HAxeModelForLevel(level, realm); name = GeneratedUniqueItem.GetNameFromId(model); }
                    else { model = GetAxeModelForLevel(level, realm); name = GeneratedUniqueItem.GetNameFromId(model); }
                    break;
                case eObjectType.Blades:
                case eObjectType.SlashingWeapon:
                    model = GetBladeModelForLevel(level, type == eObjectType.Blades ? eRealm.Hibernia : eRealm.Albion);
                    name = GeneratedUniqueItem.GetNameFromId(model);
                    if (spdAbs <= (type == eObjectType.Blades ? 27 : 31) || spdAbs < 32) { newHand = 2; slot = eInventorySlot.LeftHandWeapon; }
                    break;
                case eObjectType.Blunt:
                case eObjectType.CrushingWeapon:
                    model = GetBluntModelForLevel(level, type == eObjectType.Blunt ? eRealm.Hibernia : eRealm.Albion);
                    name = GeneratedUniqueItem.GetNameFromId(model);
                    if (spdAbs < (type == eObjectType.Blunt ? 31 : 33) || spdAbs < 35) { newHand = 2; slot = eInventorySlot.LeftHandWeapon; }
                    if (type == eObjectType.Blunt && Util.Chance(1)) { model = 3458; name = "Rolling Pin"; }
                    break;
                case eObjectType.CelticSpear:
                    model = GetSpearModelForLevel(level, eRealm.Hibernia);
                    if (spdAbs < 35) name = "Short Spear"; else if (spdAbs < 45) name = "Spear"; else if (spdAbs < 50) name = "Long Spear"; else name = "War Spear";
                    newHand = 1; slot = eInventorySlot.TwoHandWeapon;
                    break;
                case eObjectType.CompositeBow:
                    model = GetBowModelForLevel(level, eRealm.Midgard); slot = eInventorySlot.DistanceWeapon; newHand = 1;
                    name = spdAbs > 40 ? "Great Composite Bow" : "Composite Bow";
                    break;
                case eObjectType.Crossbow:
                    model = GetCrossbowModelForLevel(level, eRealm.Albion); slot = eInventorySlot.DistanceWeapon; newHand = 1; name = "Crossbow";
                    break;
                case eObjectType.Fired:
                    model = realm == eRealm.Albion ? 569 : 922; name = "Short Bow"; slot = eInventorySlot.DistanceWeapon; newHand = 1;
                    break;
                case eObjectType.Flexible:
                    model = GetFlexModelForLevel(level, eRealm.Albion, dmg);
                    if (dmg == eDamageType.Crush) { if (spdAbs < 33) name = "Morning Star"; else if (spdAbs < 40) name = "Flail"; else name = "Weighted Flail"; }
                    else { if (spdAbs < 33) name = "Whip"; else if (spdAbs < 40) name = "Chain"; else name = "War Chain"; }
                    break;
                case eObjectType.Hammer:
                    if (hand == 1) { model = Get2HHammerForLevel(level, eRealm.Midgard); name = GeneratedUniqueItem.GetNameFromId(model); }
                    else { model = GetBluntModelForLevel(level, eRealm.Midgard); name = GeneratedUniqueItem.GetNameFromId(model); }
                    break;
                case eObjectType.HandToHand:
                    model = GetH2HModelForLevel(level, eRealm.Midgard, dmg); name = GeneratedUniqueItem.GetNameFromId(model);
                    newHand = 2; slot = eInventorySlot.LeftHandWeapon; 
                    break;
                case eObjectType.Instrument:
                    model = GetInstrumentModelForLevel(level, realm); name = GeneratedUniqueItem.GetNameFromId(model); slot = eInventorySlot.DistanceWeapon; newHand = 1;
                    break;
                case eObjectType.LargeWeapons:
                    if (dmg == eDamageType.Slash) { model = Get2HSwordForLevel(level, eRealm.Hibernia); name = GeneratedUniqueItem.GetNameFromId(model); }
                    else { model = Get2HHammerForLevel(level, eRealm.Hibernia); name = (model == 474 || model == 912) ? "Big Shillelagh" : GeneratedUniqueItem.GetNameFromId(model); }
                    newHand = 1; slot = eInventorySlot.TwoHandWeapon;
                    break;
                case eObjectType.LeftAxe:
                    model = GetAxeModelForLevel(level, eRealm.Midgard); newHand = 2; slot = eInventorySlot.LeftHandWeapon;
                    if (spdAbs < 25) name = "Hand Axe"; else if (spdAbs < 30) name = "Bearded Axe"; else name = "War Axe";
                    break;
                case eObjectType.Longbow:
                    model = GetBowModelForLevel(level, eRealm.Albion); slot = eInventorySlot.DistanceWeapon; newHand = 1;
                    if (spdAbs < 44) name = "Hunting Bow"; else if (spdAbs < 55) name = "Longbow"; else name = "Heavy Longbow";
                    break;
                case eObjectType.Piercing:
                case eObjectType.ThrustWeapon:
                    model = GetThrustModelForLevel(level, type == eObjectType.Piercing ? eRealm.Hibernia : eRealm.Albion); name = GeneratedUniqueItem.GetNameFromId(model);
                    if (spdAbs < 29 || spdAbs < 30) { newHand = 2; slot = eInventorySlot.LeftHandWeapon; }
                    break;
                case eObjectType.PolearmWeapon:
                    model = GetPolearmModelForLevel(level, eRealm.Albion, dmg);
                    if (dmg == eDamageType.Slash) name = "Lochaber Axe"; else if (dmg == eDamageType.Thrust) name = "Pike"; else name = "Lucerne Hammer";
                    newHand = 1; slot = eInventorySlot.TwoHandWeapon;
                    break;
                case eObjectType.RecurvedBow:
                    model = GetBowModelForLevel(level, eRealm.Hibernia); slot = eInventorySlot.DistanceWeapon; newHand = 1;
                    name = spdAbs > 49 ? "Great Recurve Bow" : "Recurve Bow";
                    break;
                case eObjectType.Scythe:
                    model = GetScytheModelForLevel(level, eRealm.Hibernia); newHand = 1; slot = eInventorySlot.TwoHandWeapon;
                    if (spdAbs < 47) name = "Scythe"; else if (spdAbs < 51) name = "Martial Scythe"; else name = "War Scythe";
                    break;
                case eObjectType.Shield:
                    model = GetShieldModelForLevel(level, realm, (int)dmg); newHand = 2; slot = eInventorySlot.LeftHandWeapon;
                    if ((int)dmg == 1) name = "Small Shield"; else if ((int)dmg == 2) name = "Medium Shield"; else name = "Large Shield";
                    break;
                case eObjectType.Spear:
                    model = GetSpearModelForLevel(level, eRealm.Midgard); name = GeneratedUniqueItem.GetNameFromId(model);
                    newHand = 1; slot = eInventorySlot.TwoHandWeapon;
                    break;
                case eObjectType.MaulerStaff:
                    model = 19; name = "Mauler Staff"; newHand = 1; slot = eInventorySlot.TwoHandWeapon;
                    break;
                case eObjectType.Staff:
                    model = GetStaffModelForLevel(level, realm); newHand = 1; slot = eInventorySlot.TwoHandWeapon;
                    if (realm == eRealm.Albion && charClass == eCharacterClass.Friar && Util.Chance(20)) {
                        if (spdAbs < 40) name = "Quarterstaff"; else if (spdAbs < 50) name = "Shod Quarterstaff"; else name = "Heavy Shod Quarterstaff";
                    } else name = GeneratedUniqueItem.GetNameFromId(model);
                    break;
                case eObjectType.TwoHandedWeapon:
                    if (dmg == eDamageType.Slash) model = Get2HSwordForLevel(level, eRealm.Albion);
                    else if (dmg == eDamageType.Crush) model = Get2HHammerForLevel(level, eRealm.Albion);
                    else model = Get2HThrustForLevel(level, eRealm.Albion);
                    name = GeneratedUniqueItem.GetNameFromId(model); newHand = 1; slot = eInventorySlot.TwoHandWeapon;
                    break;
                case eObjectType.FistWraps:
                    string str = Util.Chance(50) ? "Hand" : "Fist"; newHand = 2; slot = eInventorySlot.LeftHandWeapon;
                    if (spdAbs < 31) { name = str + " Wrap"; model = 3476; effect = 102; }
                    else if (spdAbs < 35) { name = "Studded " + str + " Wrap"; model = 3477; effect = 48; }
                    else { name = "Spiked Fist Wrap"; model = 3478; effect = 49; }
                    break;
            }
        }

        public static List<eObjectType> GetClassWeaponTypes(eCharacterClass charClass)
        {
            List<eObjectType> types = new List<eObjectType>();

            if (charClass == eCharacterClass.Cabalist || charClass == eCharacterClass.Necromancer || charClass == eCharacterClass.Occultist || charClass == eCharacterClass.Sorcerer || charClass == eCharacterClass.Theurgist || charClass == eCharacterClass.Wizard || charClass == eCharacterClass.Acolyte || charClass == eCharacterClass.Disciple || charClass == eCharacterClass.Elementalist || charClass == eCharacterClass.Bonedancer || charClass == eCharacterClass.Runemaster || charClass == eCharacterClass.Spiritmaster || charClass == eCharacterClass.Warlock || charClass == eCharacterClass.Mage || charClass == eCharacterClass.Mystic || charClass == eCharacterClass.Eldritch || charClass == eCharacterClass.Enchanter || charClass == eCharacterClass.Mentalist || charClass == eCharacterClass.Animist || charClass == eCharacterClass.Bainshee || charClass == eCharacterClass.Forester || charClass == eCharacterClass.Magician || charClass == eCharacterClass.Seer || charClass == eCharacterClass.Friar)
            {
                types.Add(eObjectType.Staff);
                if (charClass == eCharacterClass.Healer || charClass == eCharacterClass.Shaman || charClass == eCharacterClass.Seer || charClass == eCharacterClass.Friar)
                {
                    types.Add(eObjectType.Hammer);
                    types.Add(eObjectType.CrushingWeapon);
                    types.Add(eObjectType.Shield);
                }
            }
            else if (charClass == eCharacterClass.Armsman) types.AddRange(new[] { eObjectType.PolearmWeapon, eObjectType.SlashingWeapon, eObjectType.ThrustWeapon, eObjectType.CrushingWeapon, eObjectType.TwoHandedWeapon, eObjectType.Crossbow, eObjectType.Shield });
            else if (charClass == eCharacterClass.Paladin) types.AddRange(new[] { eObjectType.SlashingWeapon, eObjectType.ThrustWeapon, eObjectType.CrushingWeapon, eObjectType.TwoHandedWeapon, eObjectType.Shield });
            else if (charClass == eCharacterClass.Reaver) types.AddRange(new[] { eObjectType.Flexible, eObjectType.SlashingWeapon, eObjectType.CrushingWeapon, eObjectType.Shield });
            else if (charClass == eCharacterClass.Minstrel) types.AddRange(new[] { eObjectType.Instrument, eObjectType.SlashingWeapon, eObjectType.ThrustWeapon, eObjectType.Shield });
            else if (charClass == eCharacterClass.Infiltrator) types.AddRange(new[] { eObjectType.SlashingWeapon, eObjectType.ThrustWeapon, eObjectType.Crossbow });
            else if (charClass == eCharacterClass.Scout) types.AddRange(new[] { eObjectType.SlashingWeapon, eObjectType.ThrustWeapon, eObjectType.Longbow, eObjectType.Shield });
            else if (charClass == eCharacterClass.Mercenary) types.AddRange(new[] { eObjectType.Fired, eObjectType.SlashingWeapon, eObjectType.ThrustWeapon, eObjectType.CrushingWeapon, eObjectType.Shield });
            else if (charClass == eCharacterClass.Cleric) types.AddRange(new[] { eObjectType.CrushingWeapon, eObjectType.Staff, eObjectType.Shield });
            else if (charClass == eCharacterClass.Heretic) types.AddRange(new[] { eObjectType.Flexible, eObjectType.CrushingWeapon, eObjectType.Shield });
            else if (charClass == eCharacterClass.MaulerAlb || charClass == eCharacterClass.MaulerMid || charClass == eCharacterClass.MaulerHib) types.AddRange(new[] { eObjectType.FistWraps, eObjectType.MaulerStaff });
            else if (charClass == eCharacterClass.AlbionRogue) types.AddRange(new[] { eObjectType.ThrustWeapon, eObjectType.Piercing });
            else if (charClass == eCharacterClass.Fighter) types.AddRange(new[] { eObjectType.SlashingWeapon, eObjectType.CrushingWeapon, eObjectType.ThrustWeapon, eObjectType.Shield, eObjectType.Sword, eObjectType.Axe, eObjectType.Hammer, eObjectType.Blades, eObjectType.Blunt });
            else if (charClass == eCharacterClass.Hunter) types.AddRange(new[] { eObjectType.Spear, eObjectType.CompositeBow, eObjectType.Sword });
            else if (charClass == eCharacterClass.Savage) types.AddRange(new[] { eObjectType.HandToHand, eObjectType.Sword, eObjectType.Axe, eObjectType.Hammer });
            else if (charClass == eCharacterClass.Shadowblade) types.AddRange(new[] { eObjectType.Sword, eObjectType.Axe, eObjectType.LeftAxe });
            else if (charClass == eCharacterClass.MidgardRogue) types.AddRange(new[] { eObjectType.Sword, eObjectType.Axe });
            else if (charClass == eCharacterClass.Berserker) types.AddRange(new[] { eObjectType.LeftAxe, eObjectType.Sword, eObjectType.Axe, eObjectType.Hammer });
            else if (charClass == eCharacterClass.Thane || charClass == eCharacterClass.Warrior || charClass == eCharacterClass.Skald) types.AddRange(new[] { eObjectType.Sword, eObjectType.Axe, eObjectType.Hammer, eObjectType.Shield });
            else if (charClass == eCharacterClass.Viking) types.AddRange(new[] { eObjectType.Sword, eObjectType.Axe, eObjectType.Hammer, eObjectType.TwoHandedWeapon, eObjectType.Shield });
            else if (charClass == eCharacterClass.Valkyrie) types.AddRange(new[] { eObjectType.Sword, eObjectType.Spear, eObjectType.Shield });
            else if (charClass == eCharacterClass.Valewalker) types.Add(eObjectType.Scythe);
            else if (charClass == eCharacterClass.Stalker || charClass == eCharacterClass.Nightshade) types.AddRange(new[] { eObjectType.Blades, eObjectType.Piercing, eObjectType.Shield });
            else if (charClass == eCharacterClass.Ranger) types.AddRange(new[] { eObjectType.Blades, eObjectType.Piercing, eObjectType.RecurvedBow });
            else if (charClass == eCharacterClass.Champion) types.AddRange(new[] { eObjectType.Blades, eObjectType.Piercing, eObjectType.Blunt, eObjectType.LargeWeapons, eObjectType.Shield });
            else if (charClass == eCharacterClass.Hero) types.AddRange(new[] { eObjectType.Blades, eObjectType.Piercing, eObjectType.Blunt, eObjectType.LargeWeapons, eObjectType.CelticSpear, eObjectType.Shield, eObjectType.Fired });
            else if (charClass == eCharacterClass.Blademaster) types.AddRange(new[] { eObjectType.Blades, eObjectType.Piercing, eObjectType.Blunt, eObjectType.Fired, eObjectType.Shield });
            else if (charClass == eCharacterClass.Warden) types.AddRange(new[] { eObjectType.Blades, eObjectType.Blunt, eObjectType.Shield, eObjectType.Fired });
            else if (charClass == eCharacterClass.Druid) types.AddRange(new[] { eObjectType.Blades, eObjectType.Blunt, eObjectType.Shield, eObjectType.Staff });
            else if (charClass == eCharacterClass.Bard) types.AddRange(new[] { eObjectType.Blades, eObjectType.Blunt, eObjectType.Shield, eObjectType.Instrument });
            else if (charClass == eCharacterClass.Naturalist) types.AddRange(new[] { eObjectType.Blades, eObjectType.Blunt, eObjectType.Shield });
            else if (charClass == eCharacterClass.Vampiir) types.Add(eObjectType.Piercing);
            else if (charClass == eCharacterClass.Guardian) types.AddRange(new[] { eObjectType.Blades, eObjectType.Blunt, eObjectType.LargeWeapons, eObjectType.Shield });
            else types.Add(eObjectType.Staff);

            return types.Distinct().ToList();
        }

        public static eDamageType GenerateDamageType(eObjectType type, eCharacterClass charClass)
        {
            switch (type)
            {
                //all
                case eObjectType.TwoHandedWeapon:
                case eObjectType.PolearmWeapon:
                case eObjectType.Instrument:
                    return (eDamageType)Util.Random(1, 3);
                //slash
                case eObjectType.Axe:
                case eObjectType.Blades:
                case eObjectType.SlashingWeapon:
                case eObjectType.LeftAxe:
                case eObjectType.Sword:
                case eObjectType.Scythe:
                    return eDamageType.Slash;
                //thrust
                case eObjectType.ThrustWeapon:
                case eObjectType.Piercing:
                case eObjectType.CelticSpear:
                case eObjectType.Longbow:
                case eObjectType.RecurvedBow:
                case eObjectType.CompositeBow:
                case eObjectType.Fired:
                case eObjectType.Crossbow:
                    return eDamageType.Thrust;
                //crush
                case eObjectType.Hammer:
                case eObjectType.CrushingWeapon:
                case eObjectType.Blunt:
                case eObjectType.MaulerStaff: //Maulers
                case eObjectType.FistWraps: //Maulers
                case eObjectType.Staff:
                    return eDamageType.Crush;
                //specifics
                case eObjectType.HandToHand:
                case eObjectType.Spear:
                    return (eDamageType)Util.Random(2, 3);
                case eObjectType.LargeWeapons:
                case eObjectType.Flexible:
                    return (eDamageType)Util.Random(1, 2);
                //do shields return the shield size?
                case eObjectType.Shield:
                    return (eDamageType)Util.Random(1, GetMaxShieldSizeFromClass(charClass));
                    //return (eDamageType)Util.Random(1, 3);
            }
            return eDamageType.Natural;
        }

        public static int GetMaxShieldSizeFromClass(eCharacterClass charClass)
        {
            //shield size is based off of damage type
            //1 = small shield
            //2 = medium
            //3 = large
            switch (charClass)
            {
                case eCharacterClass.Berserker:
                case eCharacterClass.Skald:
                case eCharacterClass.Savage:
                case eCharacterClass.Healer:
                case eCharacterClass.Shaman:
                case eCharacterClass.Shadowblade:
                case eCharacterClass.Bard:
                case eCharacterClass.Druid:
                case eCharacterClass.Nightshade:
                case eCharacterClass.Ranger:
                case eCharacterClass.Infiltrator:
                case eCharacterClass.Minstrel:
                case eCharacterClass.Scout:
                case eCharacterClass.Heretic:
                case eCharacterClass.Naturalist:
                case eCharacterClass.Seer:
                    return 1;

                case eCharacterClass.Thane:
                case eCharacterClass.Warden:
                case eCharacterClass.Blademaster:
                case eCharacterClass.Champion:
                case eCharacterClass.Mercenary:
                case eCharacterClass.Cleric:
                case eCharacterClass.Viking:
                case eCharacterClass.Guardian:
                case eCharacterClass.Fighter:
                    return 2;

                case eCharacterClass.Warrior:
                case eCharacterClass.Hero:
                case eCharacterClass.Armsman:
                case eCharacterClass.Paladin:
                case eCharacterClass.Reaver:
                case eCharacterClass.Valkyrie:
                    return 3;
                default: return 1;
            }
        }

        #endregion

        #region Armor and Weapon Pattern models

        private static int GetPossessedModel(int realm, int mat, int slot)
        {
            // MIDGARD SET (Studded 34 / Chain 35)
            if (mat == 34) // Studded
                switch (slot) { case 25: return 2707; case 27: return 2708; case 28: return 2709; case 21: return 2710; case 23: return 2711; case 22: return 2712; }
            if (mat == 35) // Chain
                switch (slot) { case 25: return 2713; case 27: return 2714; case 28: return 2715; case 21: return 2716; case 23: return 2717; case 22: return 2718; }

            // ALBION SET (Plate 36)
            if (mat == 36)
                switch (slot) { case 25: return 2753; case 27: return 2754; case 28: return 2755; case 21: return 2756; case 23: return 2757; case 22: return 2758; }

            // HIBERNIA SET (Reinforced 37 / Scale 38)
            if (mat == 37) // Reinforced
                switch (slot) { case 25: return 2772; case 27: return 2773; case 28: return 2774; case 21: return 2775; case 23: return 2776; case 22: return 2777; }
            if (mat == 38) // Scale
                switch (slot) { case 25: return 2778; case 27: return 2779; case 28: return 2780; case 21: return 2781; case 23: return 2782; case 22: return 2783; }

            // CLOTH (32)
            if (mat == 32)
            {
                if (realm == 2) // Mid
                    switch (slot) { case 25: return 2694; case 26: return 2695; case 27: return 2696; case 28: return 2697; case 21: return 2698; case 23: return 2699; case 22: return 2700; }
                if (realm == 3) // Hib
                    switch (slot) { case 25: return 2759; case 26: return 2760; case 27: return 2761; case 28: return 2762; case 21: return 2763; case 23: return 2764; case 22: return 2765; }
                // Default Alb
                switch (slot) { case 25: return 2728; case 26: return 2729; case 27: return 2730; case 28: return 2731; case 21: return 2732; case 23: return 2733; case 22: return 2734; }
            }
            // LEATHER (33)
            if (mat == 33)
            {
                if (realm == 2) // Mid
                    switch (slot) { case 25: return 2701; case 27: return 2702; case 28: return 2703; case 21: return 2704; case 23: return 2705; case 22: return 2706; }
                if (realm == 3) // Hib
                    switch (slot) { case 25: return 2766; case 27: return 2767; case 28: return 2768; case 21: return 2769; case 23: return 2770; case 22: return 2771; }
                // Default Alb
                switch (slot) { case 25: return 2735; case 27: return 2736; case 28: return 2737; case 21: return 2738; case 23: return 2739; case 22: return 2740; }
            }

            return -1;
        }

        private static int GetGoodModel(int realm, int mat, int slot)
        {
            // ALBION (1)
            if (realm == 1)
            {
                if (mat == 32) // Cloth
                    switch (slot) { case 25: return 2790; case 26: return 2791; case 27: return 2792; case 28: return 2793; case 21: return 2794; case 23: return 2795; case 22: return 2796; }
                if (mat == 33) // Leather
                    switch (slot) { case 25: return 2797; case 27: return 2798; case 28: return 2799; case 21: return 2800; case 23: return 2801; case 22: return 2802; }
                if (mat == 34) // Studded
                    switch (slot) { case 25: return 2803; case 27: return 2804; case 28: return 2805; case 21: return 2806; case 23: return 2807; case 22: return 2808; }
                if (mat == 35) // Chain
                    switch (slot) { case 25: return 2809; case 27: return 2810; case 28: return 2811; case 21: return 2812; case 23: return 2813; case 22: return 2814; }
                if (mat == 36) // Plate
                    switch (slot) { case 25: return 2815; case 27: return 2816; case 28: return 2817; case 21: return 2818; case 23: return 2819; case 22: return 2820; }
            }

            // HIBERNIA (3)
            if (realm == 3)
            {
                if (mat == 32) // Cloth
                    switch (slot) { case 25: return 2821; case 26: return 2822; case 27: return 2823; case 28: return 2824; case 21: return 2825; case 23: return 2826; case 22: return 2827; }
                if (mat == 33) // Leather
                    switch (slot) { case 25: return 2828; case 27: return 2829; case 28: return 2830; case 21: return 2831; case 23: return 2832; case 22: return 2833; }
                if (mat == 37 || mat == 34) // Reinforced/Studded
                    switch (slot) { case 25: return 2834; case 27: return 2835; case 28: return 2836; case 21: return 2837; case 23: return 2838; case 22: return 2839; }
                if (mat == 38 || mat == 35) // Scale/Chain
                    switch (slot) { case 25: return 2840; case 27: return 2841; case 28: return 2842; case 21: return 2843; case 23: return 2844; case 22: return 2845; }
                if (mat == 36) // Plate
                    switch (slot) { case 25: return 2846; case 27: return 2847; case 28: return 2848; case 21: return 2849; case 23: return 2850; case 22: return 2851; }
            }

            // MIDGARD (2)
            if (realm == 2)
            {
                if (mat == 32) // Cloth
                    switch (slot) { case 25: return 2852; case 26: return 2853; case 27: return 2854; case 28: return 2855; case 21: return 2856; case 23: return 2857; case 22: return 2858; }
                if (mat == 33) // Leather
                    switch (slot) { case 25: return 2859; case 27: return 2860; case 28: return 2861; case 21: return 2862; case 23: return 2863; case 22: return 2864; }
                if (mat == 34) // Studded
                    switch (slot) { case 25: return 2865; case 27: return 2866; case 28: return 2867; case 21: return 2868; case 23: return 2869; case 22: return 2870; }
                if (mat == 35) // Chain
                    switch (slot) { case 25: return 2871; case 27: return 2872; case 28: return 2873; case 21: return 2874; case 23: return 2875; case 22: return 2876; }
                if (mat == 36) // Plate
                    switch (slot) { case 25: return 2877; case 27: return 2878; case 28: return 2879; case 21: return 2880; case 23: return 2881; case 22: return 2882; }
            }

            return -1;
        }

        private static int GetCorruptModel(int mat, int slot)
        {
            if (mat == 32 || mat == 33) // Cloth/Leather
                switch (slot) { case 25: return 3580; case 27: return 3581; case 28: return 3582; case 22: return 3583; case 23: return 3584; case 26: return 3605; }

            if (mat == 34 || mat == 37) // Studded/Reinforced
                switch (slot) { case 25: return 3600; case 27: return 3601; case 28: return 3602; case 23: return 3603; case 22: return 3604; }

            if (mat == 35) // Chain
                switch (slot) { case 25: return 3585; case 27: return 3586; case 28: return 3587; case 22: return 3588; case 23: return 3589; }

            if (mat == 36) // Plate
                switch (slot) { case 25: return 3590; case 27: return 3591; case 28: return 3592; case 22: return 3593; case 23: return 3594; }

            if (mat == 38) // Scale
                switch (slot) { case 25: return 3595; case 27: return 3596; case 28: return 3597; case 22: return 3598; case 23: return 3599; }

            return -1;
        }

        private static int GetMinotaurModel(int mat, int slot)
        {
            if (mat == 32 || mat == 33)
                switch (slot) { case 25: return 3606; case 27: return 3607; case 28: return 3608; case 22: return 3609; case 23: return 3610; case 26: return 3631; }

            if (mat == 35)
                switch (slot) { case 25: return 3611; case 27: return 3612; case 28: return 3613; case 22: return 3614; case 23: return 3615; }

            if (mat == 36)
                switch (slot) { case 25: return 3616; case 27: return 3617; case 28: return 3618; case 22: return 3619; case 23: return 3620; }

            if (mat == 38)
                switch (slot) { case 25: return 3621; case 27: return 3622; case 28: return 3623; case 22: return 3624; case 23: return 3625; }

            if (mat == 34 || mat == 37)
                switch (slot) { case 25: return 3626; case 27: return 3627; case 28: return 3628; case 23: return 3629; case 22: return 3630; }

            return -1;
        }

        private static int GetToaModel_Oceanus(int realm, int mat, int slot)
        {
            // HELMETS
            if (slot == 21)
            {
                if (realm == 1) // ALB
                {
                    if (mat == 32) return 2253; if (mat == 33) return 2256; if (mat == 35) return 2259;
                    if (mat == 34) return 2262; if (mat == 38) return 2265; if (mat == 36) return 2268;
                }
                if (realm == 2) // MID
                {
                    if (mat == 32) return 2271; if (mat == 33) return 2274; if (mat == 35) return 2277;
                    if (mat == 34) return 2280; if (mat == 38) return 2283; if (mat == 36) return 2286;
                }
                if (realm == 3) // HIB
                {
                    if (mat == 32) return 2289; if (mat == 33) return 2292; if (mat == 35) return 2295;
                    if (mat == 34) return 2298; if (mat == 38 || mat == 37) return 2301; if (mat == 36) return 2304;
                }
            }

            // Cloth (32)
            if (mat == 32)
                switch (slot) { case 25: return (realm == 1 ? 1626 : (realm == 2 ? 1627 : 1628)); case 27: return (realm == 3 ? 1632 : 1631); case 28: return 1625; case 23: return (realm == 1 ? 1629 : 1630); }

            // Leather (33)
            if (mat == 33)
                switch (slot) { case 25: return (realm == 1 ? 1640 : (realm == 2 ? 1641 : 1642)); case 27: return (realm == 3 ? 1647 : 1646); case 28: return 1639; case 22: return 1645; case 23: return (realm == 1 ? 1643 : 1644); }

            // Studded (34) / Reinforced (37)
            if (mat == 34 || mat == 37)
                switch (slot) { case 25: return (realm == 1 ? 1848 : (realm == 2 ? 1849 : 1850)); case 27: return (realm == 3 ? 1855 : 1854); case 28: return 1847; case 22: return 1853; case 23: return (realm == 1 ? 1851 : 1852); }

            // Chain (35)
            if (mat == 35)
                switch (slot) { case 25: return (realm == 1 ? 2101 : (realm == 2 ? 2102 : 2103)); case 27: return (realm == 3 ? 2108 : 2107); case 28: return 2100; case 22: return 2106; case 23: return (realm == 1 ? 2104 : 2105); }

            // Scale (38)
            if (mat == 38)
                switch (slot) { case 25: return (realm == 1 ? 1771 : (realm == 2 ? 1772 : 1773)); case 27: return (realm == 3 ? 1778 : 1777); case 28: return 1770; case 22: return 1776; case 23: return (realm == 1 ? 1774 : 1775); }

            // Plate (36)
            if (mat == 36)
                switch (slot) { case 25: return (realm == 1 ? 2092 : (realm == 2 ? 2093 : 2094)); case 27: return (realm == 3 ? 2099 : 2098); case 28: return 2091; case 22: return 2097; case 23: return (realm == 1 ? 2095 : 2096); }

            return -1;
        }

        private static int GetToaModel_Stygia(int realm, int mat, int slot)
        {
            // HELMETS
            if (slot == 21)
            {
                if (realm == 1)
                { // Alb
                    if (mat == 32) return 2307; if (mat == 33) return 2310; if (mat == 35) return 2313;
                    if (mat == 34) return 2316; if (mat == 38) return 2319; if (mat == 36) return 2322;
                }
                if (realm == 2)
                { // Mid
                    if (mat == 32) return 2325; if (mat == 33) return 2328; if (mat == 35) return 2331;
                    if (mat == 34) return 2334; if (mat == 38) return 2337; if (mat == 36) return 2340;
                }
                if (realm == 3)
                { // Hib
                    if (mat == 32) return 2343; if (mat == 33) return 2346; if (mat == 35) return 2349;
                    if (mat == 34) return 2352; if (mat == 38) return 2355; if (mat == 36) return 2358;
                }
            }

            // Cloth (32)
            if (mat == 32)
                switch (slot) { case 25: return (realm == 1 ? 2153 : (realm == 2 ? 2154 : 2155)); case 26: return (realm == 1 ? 2160 : 0); case 27: return (realm == 3 ? 2159 : 2158); case 28: return 2152; case 23: return (realm == 1 ? 2156 : 2157); }

            // Leather (33)
            if (mat == 33)
                switch (slot) { case 25: return (realm == 1 ? 2135 : (realm == 2 ? 2136 : 2137)); case 27: return (realm == 3 ? 2142 : 2141); case 28: return 2134; case 22: return 2140; case 23: return (realm == 1 ? 2138 : 2139); }

            // Studded (34) / Reinforced (37)
            if (mat == 34 || mat == 37)
                switch (slot) { case 25: return (realm == 1 ? 1757 : (realm == 2 ? 1758 : 1759)); case 27: return (realm == 3 ? 1764 : 1763); case 28: return 1756; case 22: return 1762; case 23: return (realm == 1 ? 1760 : 1761); }

            // Scale (38)
            if (mat == 38)
                switch (slot) { case 25: return (realm == 1 ? 1789 : (realm == 2 ? 1790 : 1791)); case 27: return (realm == 3 ? 1796 : 1795); case 28: return 1788; case 22: return 1794; case 23: return (realm == 1 ? 1792 : 1793); }

            // Plate (36)
            if (mat == 36)
                switch (slot) { case 25: return (realm == 1 ? 2124 : (realm == 2 ? 2125 : 2126)); case 27: return (realm == 3 ? 2131 : 2130); case 28: return 2123; case 22: return 2129; case 23: return (realm == 1 ? 2127 : 2128); }

            return -1;
        }

        private static int GetToaModel_Volcanus(int realm, int mat, int slot)
        {
            // HELMETS
            if (slot == 21)
            {
                if (realm == 1)
                { // Alb
                    if (mat == 32) return 2361; if (mat == 33) return 2364; if (mat == 35) return 2367;
                    if (mat == 34) return 2370; if (mat == 38) return 2373; if (mat == 36) return 2376;
                }
                if (realm == 2)
                { // Mid
                    if (mat == 32) return 2379; if (mat == 33) return 2382; if (mat == 35) return 2385;
                    if (mat == 34) return 2388; if (mat == 38) return 2391; if (mat == 36) return 2394;
                }
                if (realm == 3)
                { // Hib
                    if (mat == 32) return 2397; if (mat == 33) return 2400; if (mat == 35) return 2403;
                    if (mat == 34) return 2406; if (mat == 38) return 2409; if (mat == 36) return 2412;
                }
            }

            // Cloth (32)
            if (mat == 32)
                switch (slot) { case 25: return (realm == 1 ? 2162 : (realm == 2 ? 2163 : 2164)); case 26: return (realm == 1 ? 2169 : (realm == 2 ? 2170 : 2171)); case 27: return (realm == 3 ? 2168 : 2167); case 28: return 2161; case 23: return (realm == 1 ? 2165 : 2166); }

            // Leather (33)
            if (mat == 33)
                switch (slot) { case 25: return (realm == 1 ? 2176 : (realm == 2 ? 2177 : 2178)); case 27: return (realm == 3 ? 2183 : 2182); case 28: return 2175; case 22: return 2181; case 23: return (realm == 1 ? 2179 : 2180); }

            // Studded (34)
            if (mat == 34 || mat == 37)
                switch (slot) { case 25: return (realm == 1 ? 1780 : (realm == 2 ? 1781 : 1782)); case 27: return (realm == 3 ? 1787 : 1786); case 28: return 1779; case 22: return 1785; case 23: return (realm == 1 ? 1783 : 1784); }

            // Chain (35)
            if (mat == 35)
                switch (slot) { case 25: return (realm == 1 ? 1694 : (realm == 2 ? 1695 : 1696)); case 27: return (realm == 3 ? 1701 : 1700); case 28: return 1693; case 22: return 1699; case 23: return (realm == 1 ? 1697 : 1698); }

            // Scale (38)
            if (mat == 38)
                switch (slot) { case 25: return (realm == 1 ? 1712 : (realm == 2 ? 1713 : 1714)); case 27: return (realm == 3 ? 1719 : 1718); case 28: return 1711; case 22: return 1717; case 23: return (realm == 1 ? 1715 : 1716); }

            // Plate (36)
            if (mat == 36)
                switch (slot) { case 25: return (realm == 1 ? 1703 : (realm == 2 ? 1704 : 1705)); case 27: return (realm == 3 ? 1710 : 1709); case 28: return 1702; case 22: return 1708; case 23: return (realm == 1 ? 1706 : 1707); }

            return -1;
        }

        private static int GetToaModel_Aerus(int realm, int mat, int slot)
        {
            // HELMETS
            if (slot == 21)
            {
                if (realm == 1)
                { // Alb
                    if (mat == 32) return 2415; if (mat == 33) return 2418; if (mat == 35) return 2421;
                    if (mat == 34) return 2424; if (mat == 38) return 2427; if (mat == 36) return 2430;
                }
                if (realm == 2)
                { // Mid
                    if (mat == 32) return 2433; if (mat == 33) return 2436; if (mat == 35) return 2439;
                    if (mat == 34) return 2442; if (mat == 38) return 2445; if (mat == 36) return 2448;
                }
                if (realm == 3)
                { // Hib
                    if (mat == 32) return 2451; if (mat == 33) return 2454; if (mat == 35) return 2457;
                    if (mat == 34) return 2460; if (mat == 38) return 2463; if (mat == 36) return 2466;
                }
            }

            // Cloth (32)
            if (mat == 32)
                switch (slot) { case 25: return (realm == 1 ? 2238 : (realm == 2 ? 2239 : 2240)); case 26: return (realm == 1 ? 2245 : (realm == 3 ? 2246 : 0)); case 27: return (realm == 3 ? 2244 : 2243); case 28: return 2237; case 23: return (realm == 1 ? 2241 : 2242); }

            // Leather (33)
            if (mat == 33)
                switch (slot) { case 25: return (realm == 1 ? 2144 : (realm == 2 ? 2145 : 2146)); case 27: return (realm == 3 ? 2151 : 2150); case 28: return 2143; case 22: return 2149; case 23: return (realm == 1 ? 2147 : 2148); }

            // Studded (34)
            if (mat == 34 || mat == 37)
                switch (slot) { case 25: return (realm == 1 ? 1798 : (realm == 2 ? 1799 : 1800)); case 27: return (realm == 3 ? 1805 : 1804); case 28: return 1797; case 22: return 1803; case 23: return (realm == 1 ? 1801 : 1802); }

            // Chain (35)
            if (mat == 35)
                switch (slot) { case 25: return (realm == 1 ? 1736 : (realm == 2 ? 1737 : 1738)); case 27: return (realm == 3 ? 1743 : 1742); case 28: return 1735; case 22: return 1741; case 23: return (realm == 1 ? 1739 : 1740); }

            // Scale (38)
            if (mat == 38)
                switch (slot) { case 25: return (realm == 1 ? 1748 : (realm == 2 ? 1749 : 1750)); case 27: return (realm == 3 ? 1755 : 1754); case 28: return 1747; case 22: return 1753; case 23: return (realm == 1 ? 1751 : 1752); }

            // Plate (36)
            if (mat == 36)
                switch (slot) { case 25: return (realm == 1 ? 1685 : (realm == 2 ? 1686 : 1687)); case 27: return (realm == 3 ? 1692 : 1691); case 28: return 1684; case 22: return 1690; case 23: return (realm == 1 ? 1688 : 1689); }

            return -1;
        }

        #endregion 

        #region Leather Model Generation
        public static int GetLeatherTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(31);
                    if (Level > 20)
                        validModels.Add(36);
                    if (Level > 30)
                        validModels.Add(74);
                    if (Level > 40)
                        validModels.Add(134);
                    if (Level > 50)
                        validModels.Add(2797);
                    break;
                case eRealm.Midgard:
                    validModels.Add(240);
                    if (Level > 20)
                        validModels.Add(260);
                    if (Level > 30)
                        validModels.Add(280);
                    if (Level > 40)
                        validModels.Add(300);
                    if (Level > 50)
                        validModels.Add(2859);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(373);
                    if (Level >= 10)
                        validModels.Add(393);
                    if (Level >= 20)
                        validModels.Add(413);
                    if (Level >= 30)
                        validModels.Add(433);
                    if (Level >= 40)
                        validModels.Add(2988);
                    if (Level > 50)
                        validModels.Add(2828);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetLeatherPantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(32);
                    if (Level > 20)
                        validModels.Add(37);
                    if (Level > 30)
                        validModels.Add(75);
                    if (Level > 40)
                        validModels.Add(135);
                    if (Level > 50)
                        validModels.Add(2798);
                    break;
                case eRealm.Midgard:
                    validModels.Add(241);
                    if (Level > 20)
                        validModels.Add(261);
                    if (Level > 30)
                        validModels.Add(281);
                    if (Level > 40)
                        validModels.Add(301);
                    if (Level > 50)
                        validModels.Add(2860);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(374);
                    if (Level >= 10)
                        validModels.Add(394);
                    if (Level >= 20)
                        validModels.Add(414);
                    if (Level >= 30)
                        validModels.Add(434);
                    if (Level >= 40)
                        validModels.Add(1257);
                    if (Level > 50)
                        validModels.Add(2829);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetLeatherSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(33);
                    if (Level > 20)
                        validModels.Add(38);
                    if (Level > 30)
                        validModels.Add(76);
                    if (Level > 40)
                        validModels.Add(136);
                    if (Level > 50)
                        validModels.Add(2799);
                    break;
                case eRealm.Midgard:
                    validModels.Add(242);
                    if (Level > 20)
                        validModels.Add(262);
                    if (Level > 30)
                        validModels.Add(282);
                    if (Level > 40)
                        validModels.Add(302);
                    if (Level > 50)
                        validModels.Add(2861);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(375);
                    if (Level >= 10)
                        validModels.Add(395);
                    if (Level >= 20)
                        validModels.Add(415);
                    if (Level >= 30)
                        validModels.Add(435);
                    if (Level >= 40)
                        validModels.Add(355);
                    if (Level > 50)
                        validModels.Add(2830);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetLeatherHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(34);
                    if (Level > 20)
                        validModels.Add(39);
                    if (Level > 30)
                        validModels.Add(77);
                    if (Level > 40)
                        validModels.Add(137);
                    if (Level > 50)
                        validModels.Add(2802);
                    break;
                case eRealm.Midgard:
                    validModels.Add(243);
                    if (Level > 20)
                        validModels.Add(263);
                    if (Level > 30)
                        validModels.Add(283);
                    if (Level > 40)
                        validModels.Add(303);
                    if (Level > 50)
                        validModels.Add(2864);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(376);
                    if (Level >= 10)
                        validModels.Add(396);
                    if (Level >= 20)
                        validModels.Add(416);
                    if (Level >= 30)
                        validModels.Add(436);
                    if (Level >= 40)
                        validModels.Add(1259);
                    if (Level > 50)
                        validModels.Add(2833);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetLeatherBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(40);
                    if (Level > 20)
                        validModels.Add(133);
                    if (Level > 30)
                        validModels.Add(78);
                    if (Level > 40)
                        validModels.Add(138);
                    if (Level > 50)
                        validModels.Add(2801);
                    break;
                case eRealm.Midgard:
                    validModels.Add(244);
                    if (Level > 20)
                        validModels.Add(264);
                    if (Level > 30)
                        validModels.Add(284);
                    if (Level > 40)
                        validModels.Add(304);
                    if (Level > 50)
                        validModels.Add(2863);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(377);
                    if (Level >= 10)
                        validModels.Add(397);
                    if (Level >= 20)
                        validModels.Add(417);
                    if (Level >= 30)
                        validModels.Add(437);
                    if (Level >= 40)
                        validModels.Add(1260);
                    if (Level > 50)
                        validModels.Add(2832);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetLeatherHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(62);
                    if (Level > 35)
                        validModels.Add(1231);
                    if (Level > 45)
                        validModels.Add(2800);
                    if (Level > 50)
                        validModels.Add(1232);
                    break;
                case eRealm.Midgard:
                    validModels.Add(335);
                    if (Level > 35)
                        validModels.Add(336);
                    if (Level > 45)
                        validModels.Add(337);
                    if (Level > 50)
                        validModels.Add(1214);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(438);
                    if (Level > 35)
                        validModels.Add(439);
                    if (Level > 45)
                        validModels.Add(440);
                    if (Level > 50)
                        validModels.Add(1198);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Studded Model Generation
        public static int GetStuddedTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(51);
                    if (Level > 20)
                        validModels.Add(81);
                    if (Level > 30)
                        validModels.Add(156);
                    if (Level > 40)
                        validModels.Add(216);
                    if (Level > 50)
                        validModels.Add(2803);
                    break;
                case eRealm.Midgard:
                    validModels.Add(230);
                    if (Level > 20)
                        validModels.Add(250);
                    if (Level > 30)
                        validModels.Add(270);
                    if (Level > 40)
                        validModels.Add(3012);
                    if (Level > 50)
                        validModels.Add(2865);
                    break;
                default:
                    validModels.Add(0);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStuddedPantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(52);
                    if (Level > 20)
                        validModels.Add(82);
                    if (Level > 30)
                        validModels.Add(217);
                    if (Level > 40)
                        validModels.Add(157);
                    if (Level > 50)
                        validModels.Add(2804);
                    break;
                case eRealm.Midgard:
                    validModels.Add(231);
                    if (Level > 20)
                        validModels.Add(251);
                    if (Level > 30)
                        validModels.Add(271);
                    if (Level > 40)
                        validModels.Add(291);
                    if (Level > 50)
                        validModels.Add(2866);
                    break;
                default:
                    validModels.Add(52);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStuddedSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(53);
                    if (Level > 20)
                        validModels.Add(83);
                    if (Level > 30)
                        validModels.Add(218);
                    if (Level > 40)
                        validModels.Add(158);
                    if (Level > 50)
                        validModels.Add(2805);
                    break;
                case eRealm.Midgard:
                    validModels.Add(232);
                    if (Level > 20)
                        validModels.Add(252);
                    if (Level > 30)
                        validModels.Add(272);
                    if (Level > 40)
                        validModels.Add(292);
                    if (Level > 50)
                        validModels.Add(2867);
                    break;
                default:
                    validModels.Add(53);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStuddedHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(80);
                    if (Level > 20)
                        validModels.Add(85);
                    if (Level > 30)
                        validModels.Add(219);
                    if (Level > 40)
                        validModels.Add(159);
                    if (Level > 50)
                        validModels.Add(2808);
                    break;
                case eRealm.Midgard:
                    validModels.Add(233);
                    if (Level > 20)
                        validModels.Add(253);
                    if (Level > 30)
                        validModels.Add(273);
                    if (Level > 40)
                        validModels.Add(293);
                    if (Level > 50)
                        validModels.Add(2870);
                    break;
                default:
                    validModels.Add(80);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStuddedBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(54);
                    if (Level > 20)
                        validModels.Add(84);
                    if (Level > 30)
                        validModels.Add(220);
                    if (Level > 40)
                        validModels.Add(160);
                    if (Level > 50)
                        validModels.Add(2807);
                    break;
                case eRealm.Midgard:
                    validModels.Add(234);
                    if (Level > 20)
                        validModels.Add(254);
                    if (Level > 30)
                        validModels.Add(274);
                    if (Level > 40)
                        validModels.Add(294);
                    if (Level > 50)
                        validModels.Add(2869);
                    break;
                default:
                    validModels.Add(54);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStuddedHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(824);
                    if (Level > 35)
                        validModels.Add(1233);
                    if (Level > 45)
                        validModels.Add(1234);
                    if (Level > 50)
                        validModels.Add(1235);
                    break;
                case eRealm.Midgard:
                    validModels.Add(829);
                    if (Level > 35)
                        validModels.Add(830);
                    if (Level > 45)
                        validModels.Add(831);
                    if (Level > 50)
                        validModels.Add(1215);
                    break;
                default:
                    validModels.Add(824);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Chain Model Generation
        public static int GetChainTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(41);
                    if (Level > 10)
                        validModels.Add(181);
                    if (Level > 20)
                        validModels.Add(186);
                    if (Level > 30)
                        validModels.Add(191);
                    if (Level > 40)
                        validModels.Add(1251);
                    if (Level > 50)
                        validModels.Add(1246);
                    break;
                case eRealm.Midgard:
                    validModels.Add(235);
                    if (Level > 10)
                        validModels.Add(255);
                    if (Level > 20)
                        validModels.Add(275);
                    if (Level > 30)
                        validModels.Add(295);
                    if (Level > 40)
                        validModels.Add(999);
                    if (Level > 50)
                        validModels.Add(1262);
                    break;
                default:
                    validModels.Add(41);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetChainPantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(42);
                    if (Level > 10)
                        validModels.Add(1252);
                    if (Level > 20)
                        validModels.Add(182);
                    if (Level > 30)
                        validModels.Add(187);
                    if (Level > 40)
                        validModels.Add(192);
                    if (Level > 50)
                        validModels.Add(1247);
                    break;
                case eRealm.Midgard:
                    validModels.Add(236);
                    if (Level > 20)
                        validModels.Add(256);
                    if (Level > 30)
                        validModels.Add(276);
                    if (Level > 40)
                        validModels.Add(998);
                    if (Level > 50)
                        validModels.Add(1261);
                    break;
                default:
                    validModels.Add(236);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetChainSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(43);
                    if (Level > 20)
                        validModels.Add(183);
                    if (Level > 30)
                        validModels.Add(188);
                    if (Level > 40)
                        validModels.Add(193);
                    if (Level > 50)
                        validModels.Add(1265);
                    break;
                case eRealm.Midgard:
                    validModels.Add(237);
                    if (Level > 20)
                        validModels.Add(257);
                    if (Level > 30)
                        validModels.Add(277);
                    if (Level > 40)
                        validModels.Add(1002);
                    if (Level > 50)
                        validModels.Add(1265);
                    break;
                default:
                    validModels.Add(237);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetChainHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(44);
                    if (Level > 20)
                        validModels.Add(184);
                    if (Level > 30)
                        validModels.Add(189);
                    if (Level > 40)
                        validModels.Add(194);
                    if (Level > 50)
                        validModels.Add(1249);
                    break;
                case eRealm.Midgard:
                    validModels.Add(238);
                    if (Level > 20)
                        validModels.Add(258);
                    if (Level > 30)
                        validModels.Add(278);
                    if (Level > 40)
                        validModels.Add(1000);
                    if (Level > 50)
                        validModels.Add(1263);
                    break;
                default:
                    validModels.Add(44);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetChainBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(45);
                    if (Level > 20)
                        validModels.Add(185);
                    if (Level > 30)
                        validModels.Add(190);
                    if (Level > 40)
                        validModels.Add(1250);
                    if (Level > 50)
                        validModels.Add(1255);
                    break;
                case eRealm.Midgard:
                    validModels.Add(239);
                    if (Level > 20)
                        validModels.Add(259);
                    if (Level > 30)
                        validModels.Add(279);
                    if (Level > 40)
                        validModels.Add(1001);
                    if (Level > 50)
                        validModels.Add(1264);
                    break;
                default:
                    validModels.Add(45);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetChainHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(1236);
                    if (Level > 35)
                        validModels.Add(63);
                    if (Level > 45)
                        validModels.Add(2812);
                    break;
                case eRealm.Midgard:
                    validModels.Add(832);
                    if (Level > 35)
                        validModels.Add(833);
                    if (Level > 45)
                        validModels.Add(834);
                    if (Level > 50)
                        validModels.Add(1216);
                    break;
                default:
                    validModels.Add(1236);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Plate Model Generation
        public static int GetPlateTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(46);
                    if (Level > 20)
                        validModels.Add(86);
                    if (Level > 30)
                        validModels.Add(201);
                    if (Level > 40)
                        validModels.Add(206);
                    if (Level > 50)
                    {
                        validModels.Add(1272);
                        validModels.Add(2815);
                    }
                    break;
                default:
                    validModels.Add(0);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPlatePantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(47);
                    if (Level > 20)
                        validModels.Add(87);
                    if (Level > 30)
                        validModels.Add(202);
                    if (Level > 40)
                        validModels.Add(207);
                    if (Level > 50)
                    {
                        validModels.Add(1273);
                        validModels.Add(2816);
                    }
                    break;
                default:
                    validModels.Add(47);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPlateSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(48);
                    if (Level > 20)
                        validModels.Add(88);
                    if (Level > 30)
                        validModels.Add(203);
                    if (Level > 40)
                        validModels.Add(208);
                    if (Level > 50)
                    {
                        validModels.Add(1274);
                        validModels.Add(2817);
                    }
                    break;
                default:
                    validModels.Add(48);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPlateHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(49);
                    if (Level > 20)
                        validModels.Add(89);
                    if (Level > 30)
                        validModels.Add(204);
                    if (Level > 40)
                        validModels.Add(209);
                    if (Level > 50)
                        validModels.Add(2820);
                    break;
                default:
                    validModels.Add(49);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPlateBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(50);
                    if (Level > 20)
                        validModels.Add(90);
                    if (Level > 30)
                        validModels.Add(205);
                    if (Level > 40)
                        validModels.Add(210);
                    if (Level > 50)
                        validModels.Add(2819);
                    break;
                default:
                    validModels.Add(50);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPlateHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(64);
                    if (Level > 10)
                        validModels.Add(93);
                    if (Level > 35)
                        validModels.Add(1238);
                    if (Level > 45)
                        validModels.Add(1239);
                    if (Level > 50)
                        validModels.Add(95);
                    break;
                default:
                    validModels.Add(64);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Reinforced Model Generation
        public static int GetReinforcedTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(363);
                    if (Level > 10)
                        validModels.Add(383);
                    if (Level > 20)
                        validModels.Add(403);
                    if (Level > 30)
                        validModels.Add(423);
                    if (Level > 40)
                        validModels.Add(1256);
                    if (Level > 50)
                        validModels.Add(3012);
                    break;
                default:
                    validModels.Add(363);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetReinforcedPantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(364);
                    if (Level > 10)
                        validModels.Add(384);
                    if (Level > 20)
                        validModels.Add(404);
                    if (Level > 30)
                        validModels.Add(424);
                    if (Level > 40)
                        validModels.Add(1257);
                    if (Level > 50)
                        validModels.Add(3013);
                    break;
                default:
                    validModels.Add(364);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetReinforcedSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(365);
                    if (Level > 10)
                        validModels.Add(385);
                    if (Level > 20)
                        validModels.Add(405);
                    if (Level > 30)
                        validModels.Add(425);
                    if (Level > 40)
                        validModels.Add(1258);
                    if (Level > 50)
                        validModels.Add(3014);
                    break;
                default:
                    validModels.Add(365);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetReinforcedHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(366);
                    if (Level > 10)
                        validModels.Add(386);
                    if (Level > 20)
                        validModels.Add(406);
                    if (Level > 30)
                        validModels.Add(426);
                    if (Level > 40)
                        validModels.Add(1259);
                    if (Level > 50)
                        validModels.Add(3016);
                    break;
                default:
                    validModels.Add(366);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetReinforcedBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(367);
                    if (Level > 10)
                        validModels.Add(387);
                    if (Level > 20)
                        validModels.Add(407);
                    if (Level > 30)
                        validModels.Add(427);
                    if (Level > 40)
                        validModels.Add(1260);
                    if (Level > 50)
                        validModels.Add(3015);
                    break;
                default:
                    validModels.Add(50);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetReinforcedHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(835);
                    if (Level > 10)
                        validModels.Add(836);
                    if (Level > 35)
                        validModels.Add(837);
                    if (Level > 45)
                        validModels.Add(1199);
                    if (Level > 50)
                        validModels.Add(2837);
                    break;
                default:
                    validModels.Add(64);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Scale Model Generation
        public static int GetScaleTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(368);
                    if (Level > 10)
                        validModels.Add(388);
                    if (Level > 20)
                        validModels.Add(408);
                    if (Level > 30)
                        validModels.Add(428);
                    if (Level > 40)
                        validModels.Add(988);
                    if (Level > 50)
                        validModels.Add(3000);
                    break;
                default:
                    validModels.Add(368);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScalePantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(369);
                    if (Level > 10)
                        validModels.Add(389);
                    if (Level > 20)
                        validModels.Add(409);
                    if (Level > 30)
                        validModels.Add(429);
                    if (Level > 40)
                        validModels.Add(989);
                    if (Level > 50)
                        validModels.Add(3001);
                    break;
                default:
                    validModels.Add(369);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScaleSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(370);
                    if (Level > 10)
                        validModels.Add(390);
                    if (Level > 20)
                        validModels.Add(410);
                    if (Level > 30)
                        validModels.Add(430);
                    if (Level > 40)
                        validModels.Add(990);
                    if (Level > 50)
                        validModels.Add(3002);
                    break;
                default:
                    validModels.Add(365);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScaleHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(371);
                    if (Level > 10)
                        validModels.Add(391);
                    if (Level > 20)
                        validModels.Add(411);
                    if (Level > 30)
                        validModels.Add(431);
                    if (Level > 40)
                        validModels.Add(991);
                    if (Level > 50)
                        validModels.Add(3005);
                    break;
                default:
                    validModels.Add(371);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScaleBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(372);
                    if (Level > 10)
                        validModels.Add(392);
                    if (Level > 20)
                        validModels.Add(412);
                    if (Level > 30)
                        validModels.Add(432);
                    if (Level > 40)
                        validModels.Add(992);
                    if (Level > 50)
                        validModels.Add(3004);
                    break;
                default:
                    validModels.Add(372);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScaleHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(838);
                    if (Level > 10)
                        validModels.Add(839);
                    if (Level > 35)
                        validModels.Add(840);
                    if (Level > 45)
                        validModels.Add(1200);
                    if (Level > 50)
                        validModels.Add(2843);
                    break;
                default:
                    validModels.Add(838);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Weapon Model Generation

        public static int Get2HAxeModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(9);
                    if (Level > 10)
                        validModels.Add(72);
                    if (Level > 30)
                        validModels.Add(73);
                    break;
                case eRealm.Midgard:
                    validModels.Add(317);
                    if (Level > 10)
                    {
                        validModels.Add(318);
                    }
                    if (Level > 20)
                        validModels.Add(1030);
                    if (Level > 30)
                    {
                        validModels.Add(955);
                        validModels.Add(1033);
                    }
                    if (Level > 40)
                        validModels.Add(1027);

                    if (Level > 50)
                        validModels.Add(660);

                    break;
                default:
                    validModels.Add(2);
                    break;
            }


            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3662);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3705);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }
        public static int GetAxeModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(2);
                    if (Level > 10)
                        validModels.Add(878);
                    if (Level > 30)
                        validModels.Add(880);
                    if (Level > 40)
                        validModels.Add(3681);
                    if (Level > 50)
                        validModels.Add(3724);
                    break;
                case eRealm.Midgard:
                    validModels.Add(315);
                    validModels.Add(316);
                    if (Level > 10)
                    {
                        validModels.Add(319);
                        validModels.Add(573);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(951);
                        validModels.Add(953);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(1010);
                        validModels.Add(1011);
                    }

                    if (Level > 50)
                    {
                        validModels.Add(1014);
                        validModels.Add(1018);
                        validModels.Add(654);
                    }

                    if (Util.Chance(1) && Level > 40)
                    {
                        validModels.Clear();
                        validModels.Add(3681);
                        validModels.Add(3680);
                    }
                    if (Util.Chance(1) && Level > 50)
                    {
                        validModels.Clear();
                        validModels.Add(3723);
                        validModels.Add(3724);
                    }

                    break;
                default:
                    validModels.Add(2);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3662);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3705);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int Get2HSwordForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(459);
                    if (Level > 10)
                        validModels.Add(448);
                    if (Level > 20)
                        validModels.Add(639);
                    if (Level > 30)
                        validModels.Add(907);
                    if (Level > 40)
                    {
                        validModels.Add(910);
                        validModels.Add(3658);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(911);
                        validModels.Add(3701);
                    }
                    break;
                case eRealm.Albion:
                    validModels.Add(6);
                    if (Level > 10)
                        validModels.Add(7);
                    if (Level > 20)
                        validModels.Add(9);
                    if (Level > 30)
                        validModels.Add(72);
                    if (Level > 40)
                    {
                        validModels.Add(73);
                        validModels.Add(645);
                        validModels.Add(841);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(843);
                        validModels.Add(845);
                        validModels.Add(847);

                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(314);
                    if (Level > 10)
                        validModels.Add(572);
                    if (Level > 20)
                        validModels.Add(658);
                    if (Level > 30)
                        validModels.Add(1035);
                    if (Level > 40)
                    {
                        validModels.Add(957);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(1032);
                    }
                    break;
                default:
                    validModels.Add(6);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3658);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3701);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetBladeModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(444);
                    validModels.Add(445);
                    if (Level > 10)
                    {
                        validModels.Add(446);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(447);

                    }
                    if (Level > 40)
                    {
                        validModels.Add(460);
                    }

                    if (Level > 50)
                    {
                        validModels.Add(473);
                    }
                    break;
                case eRealm.Albion:
                    validModels.Add(1);
                    validModels.Add(3);
                    if (Level > 10)
                    {
                        validModels.Add(4);
                        validModels.Add(5);
                    }
                    if (Level > 20)
                        validModels.Add(8);
                    if (Level > 30)
                    {
                        validModels.Add(10);
                        validModels.Add(652);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(877);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(879);
                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(311);
                    if (Level > 10)
                    {
                        validModels.Add(310);
                        validModels.Add(312);
                    }
                    if (Level > 20)
                        validModels.Add(313);
                    if (Level > 30)
                        validModels.Add(949);
                    if (Level > 40)
                    {
                        validModels.Add(948);
                        validModels.Add(952);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(655);
                        validModels.Add(1017);
                        validModels.Add(1015);
                    }
                    break;
                default:
                    validModels.Add(445);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3675);
                validModels.Add(3674);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3717);
                validModels.Add(3718);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int Get2HHammerForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(474);
                    validModels.Add(463);
                    if (Level > 10)
                    {
                        validModels.Add(462);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(640);
                        validModels.Add(912);

                    }
                    if (Level > 40)
                    {
                        validModels.Add(904);
                        validModels.Add(906);
                        validModels.Add(908);
                        validModels.Add(909);
                        validModels.Add(3661);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(905);
                        validModels.Add(917);
                        validModels.Add(905);
                        validModels.Add(3704);
                    }
                    break;
                case eRealm.Albion:
                    validModels.Add(16);

                    if (Level > 20)
                        validModels.Add(17);
                    if (Level > 30)
                        validModels.Add(644);
                    if (Level > 40)
                        validModels.Add(842);
                    if (Level > 50)
                        validModels.Add(844);
                    break;
                case eRealm.Midgard:
                    validModels.Add(574);
                    if (Level > 10)
                        validModels.Add(575);

                    if (Level > 20)
                    {
                        validModels.Add(576);
                        validModels.Add(659);
                    }

                    if (Level > 30)
                        validModels.Add(956);

                    if (Level > 40)
                    {
                        validModels.Add(1031);
                        validModels.Add(1034);
                    }
                    if (Level > 50)
                        validModels.Add(1028);
                    break;
                default:
                    validModels.Add(449);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3661);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3704);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetBluntModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(449);
                    validModels.Add(450);
                    if (Level > 10)
                        validModels.Add(451);
                    if (Level > 20)
                        validModels.Add(452);
                    if (Level > 30)
                        validModels.Add(461);
                    if (Level > 40)
                    {
                        validModels.Add(913);
                        validModels.Add(914);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(916);
                        validModels.Add(915);
                    }
                    break;
                case eRealm.Albion:
                    validModels.Add(11);
                    validModels.Add(12);
                    if (Level > 10)
                    {
                        validModels.Add(13);
                        validModels.Add(14);
                    }
                    if (Level > 20)
                        validModels.Add(15);
                    if (Level > 30)
                    {
                        validModels.Add(18);
                        validModels.Add(20);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(853);
                        validModels.Add(854);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(855);
                        validModels.Add(856);
                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(320);
                    validModels.Add(321);
                    if (Level > 10)
                    {
                        validModels.Add(322);
                        validModels.Add(323);
                    }
                    if (Level > 20)
                        validModels.Add(324);
                    if (Level > 30)
                    {
                        validModels.Add(950);
                        validModels.Add(954);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(1019);
                        validModels.Add(1016);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(1016);
                        validModels.Add(1009);
                    }
                    break;
                default:
                    validModels.Add(449);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3676);
                validModels.Add(3677);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3719);
                validModels.Add(3720);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int Get2HThrustForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(846);

                    if (Level > 20)
                        validModels.Add(646);
                    if (Level > 30)
                        validModels.Add(2661);
                    if (Level > 40)
                    {

                    }
                    if (Level > 50)
                    {
                        validModels.Add(2208);

                    }
                    break;
                default:
                    validModels.Add(846);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();

                validModels.Add(3657);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3700);
                validModels.Add(3817);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetThrustModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(71);
                    validModels.Add(454);
                    if (Level > 10)
                    {
                        validModels.Add(455);
                        validModels.Add(902);
                    }
                    if (Level > 20)
                    {
                        validModels.Add(456);
                        validModels.Add(898);
                        validModels.Add(940);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(457);
                        validModels.Add(472);
                        validModels.Add(895);
                        validModels.Add(941);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(460);
                        validModels.Add(643);
                        validModels.Add(947);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(453);
                        validModels.Add(942);
                        validModels.Add(943);
                        validModels.Add(944);
                        validModels.Add(945);
                        validModels.Add(946);
                        validModels.Add(2209);
                    }
                    break;
                case eRealm.Albion:
                    validModels.Add(21);
                    validModels.Add(71);
                    if (Level > 10)
                    {
                        validModels.Add(876);
                        validModels.Add(22);
                        //validModels.Add(23);
                    }
                    if (Level > 20)
                    {
                        validModels.Add(889);
                        validModels.Add(25);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(888);
                        validModels.Add(887);
                        //validModels.Add(29);
                        validModels.Add(30);
                    }
                    if (Level > 40)
                        validModels.Add(653);
                    if (Level > 50)
                    {
                        validModels.Add(885);
                        validModels.Add(886);
                        validModels.Add(2209);
                    }
                    break;
                default:
                    validModels.Add(1);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3721);
                validModels.Add(3722);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3721);
                validModels.Add(3722);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPolearmModelForLevel(int Level, eRealm realm, eDamageType dtype)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    switch (dtype)
                    {
                        case eDamageType.Slash:
                            validModels.Add(67);
                            if (Level > 10)
                                validModels.Add(68);
                            if (Level > 20)
                                validModels.Add(648);
                            if (Level > 30)
                                validModels.Add(649);
                            if (Level > 40)
                            {
                                validModels.Add(873);
                                if (Util.Chance(1))
                                {
                                    validModels.Clear();
                                    validModels.Add(3672);
                                }
                            }
                            if (Level > 50)
                            {
                                validModels.Add(874);
                                if (Util.Chance(1))
                                {
                                    validModels.Clear();
                                    validModels.Add(3715);
                                }
                            }
                            break;
                        case eDamageType.Crush:
                            validModels.Add(70);
                            if (Level > 10)
                                validModels.Add(650);
                            if (Level > 20)
                                validModels.Add(870);
                            if (Level > 30)
                                validModels.Add(875);
                            if (Level > 40 && Util.Chance(1))
                                validModels.Add(3673);
                            if (Level > 50 && Util.Chance(1))
                            {

                                validModels.Add(3833);
                                validModels.Add(3716);
                            }
                            break;
                        case eDamageType.Thrust:
                            validModels.Add(26);
                            if (Level > 10)
                                validModels.Add(69);
                            if (Level > 20)
                                validModels.Add(458);
                            if (Level > 30)
                                validModels.Add(649);
                            if (Level > 40)
                            {
                                validModels.Add(871);
                                if (Util.Chance(1))
                                {
                                    validModels.Clear();
                                    validModels.Add(3671);
                                }
                            }
                            if (Level > 50)
                            {
                                validModels.Add(872);
                                if (Util.Chance(1))
                                {
                                    validModels.Clear();
                                    validModels.Add(3714);
                                }
                            }
                            break;
                    }
                    break;
                default:
                    validModels.Add(328);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetSpearModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(556);
                    validModels.Add(469);
                    if (Level > 10)
                    {
                        validModels.Add(470);
                        validModels.Add(475);
                    }
                    if (Level > 20)
                    {
                        validModels.Add(476);
                        validModels.Add(477);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(934);
                        validModels.Add(935);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(556);
                        validModels.Add(933);
                        validModels.Add(936);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(937);
                        validModels.Add(938);
                        validModels.Add(939);
                        validModels.Add(2689);
                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(328);
                    if (Level > 10)
                        validModels.Add(329);
                    if (Level > 20)
                        validModels.Add(330);
                    if (Level > 30)
                        validModels.Add(331);
                    if (Level > 40)
                    {
                        validModels.Add(332);
                        validModels.Add(958);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(657);
                        validModels.Add(1029);
                    }
                    break;
                default:
                    validModels.Add(328);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3660);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3703);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetBowModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(471);
                    if (Level > 10)
                        validModels.Add(918);
                    if (Level > 20)
                        validModels.Add(919);
                    if (Level > 30)
                        validModels.Add(920);
                    if (Level > 40)
                    {
                        validModels.Add(921);
                        validModels.Add(922);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(923);
                        validModels.Add(925);
                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(564);
                    if (Level > 30)
                        validModels.Add(1037);
                    if (Level > 40)
                        validModels.Add(1038);
                    if (Level > 50)
                        validModels.Add(1039);
                    break;
                case eRealm.Albion:
                    validModels.Add(132);
                    if (Level > 10)
                        validModels.Add(570);
                    if (Level > 20)
                        validModels.Add(848);
                    if (Level > 30)
                        validModels.Add(849);
                    if (Level > 40)
                        validModels.Add(850);
                    if (Level > 50)
                    {
                        validModels.Add(851);
                        validModels.Add(852);
                    }
                    break;
                default:
                    validModels.Add(132);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3824);
                validModels.Add(3663);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3706);
                validModels.Add(3823);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetFlexModelForLevel(int Level, eRealm realm, eDamageType dtype)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    switch (dtype)
                    {
                        case eDamageType.Crush:
                            validModels.Add(861);
                            if (Level > 10)
                                validModels.Add(862);
                            if (Level > 20)
                                validModels.Add(864);
                            if (Level > 30)
                                validModels.Add(869);
                            if (Level > 40)
                            {
                                validModels.Add(2669);
                                if (Util.Chance(1))
                                    validModels.Add(3653);
                            }
                            if (Level > 50 && Util.Chance(1))
                            {
                                validModels.Clear();
                                validModels.Add(3696);
                                validModels.Add(3815);
                                validModels.Add(3952);
                            }
                            break;
                        case eDamageType.Slash:
                            validModels.Add(857);
                            validModels.Add(859);
                            validModels.Add(865);
                            if (Level > 10)
                                validModels.Add(863);
                            if (Level > 20)
                                validModels.Add(867);
                            if (Level > 30)
                                validModels.Add(868);
                            if (Level > 40)
                            {
                                validModels.Add(2670);
                                if (Util.Chance(1))
                                    validModels.Add(3654);
                            }
                            if (Level > 50 && Util.Chance(1))
                            {
                                validModels.Add(3697);
                                validModels.Add(3814);
                                validModels.Add(3951);
                            }
                            break;
                    }
                    break;
                default:
                    validModels.Add(132);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetH2HModelForLevel(int Level, eRealm realm, eDamageType dtype)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Midgard:
                    switch (dtype)
                    {
                        case eDamageType.Thrust:
                            validModels.Add(960);
                            validModels.Add(962);
                            validModels.Add(964);
                            if (Level > 10)
                                validModels.Add(966);
                            if (Level > 20)
                                validModels.Add(968);
                            if (Level > 30)
                                validModels.Add(970);
                            if (Level > 40)
                            {
                                validModels.Add(972);
                                validModels.Add(974);
                                validModels.Add(976);
                                if (Util.Chance(1))
                                {
                                    validModels.Add(3686);
                                    validModels.Add(3687);
                                }
                            }
                            if (Level > 50)
                            {
                                validModels.Add(978);
                                validModels.Add(980);
                                validModels.Add(982);
                                if (Util.Chance(1))
                                {
                                    validModels.Add(3729);
                                    validModels.Add(3730);
                                }
                            }
                            break;
                        case eDamageType.Slash:
                            validModels.Add(959);
                            validModels.Add(961);
                            validModels.Add(963);
                            if (Level > 10)
                                validModels.Add(965);
                            if (Level > 20)
                                validModels.Add(967);
                            if (Level > 30)
                                validModels.Add(969);
                            if (Level > 40)
                            {
                                validModels.Add(971);
                                validModels.Add(973);
                                validModels.Add(975);
                                if (Util.Chance(1))
                                {
                                    validModels.Add(3682);
                                    validModels.Add(3683);
                                }
                            }
                            if (Level > 50)
                            {
                                validModels.Add(977);
                                validModels.Add(979);
                                validModels.Add(981);
                                if (Util.Chance(1))
                                {
                                    validModels.Add(3725);
                                    validModels.Add(3726);
                                }
                            }
                            break;
                    }
                    break;
                default:
                    validModels.Add(132);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetCrossbowModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(226);
                    if (Level > 10)
                        validModels.Add(890);
                    if (Level > 20)
                        validModels.Add(891);
                    if (Level > 30)
                        validModels.Add(892);
                    if (Level > 40)
                    {
                        validModels.Add(893);
                        validModels.Add(894);
                    }
                    break;
                default:
                    validModels.Add(226);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3656);
            }
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3816);
                validModels.Add(3953);
                validModels.Add(3699);
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScytheModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(931);
                    if (Level > 10)
                        validModels.Add(929);
                    if (Level > 20)
                        validModels.Add(928);
                    if (Level > 30)
                        validModels.Add(930);
                    if (Level > 40)
                    {
                        validModels.Add(932);
                        validModels.Add(926);
                        validModels.Add(927);
                        if (Util.Chance(1))
                            validModels.Add(3665);
                    }
                    if (Level > 50 && Util.Chance(1))
                    {
                        validModels.Clear();
                        validModels.Add(3825);
                        validModels.Add(3708);
                        validModels.Add(3885);
                    }
                    break;
                default:
                    validModels.Add(931);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStaffModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(19);
                    if (Level > 10)
                        validModels.Add(442);
                    if (Level > 20)
                        validModels.Add(567);
                    if (Level > 30)
                        validModels.Add(568);
                    if (Level > 40)
                    {
                        validModels.Add(882);
                        validModels.Add(883);
                        validModels.Add(1166);
                        validModels.Add(1169);

                    }
                    if (Level > 50)
                    {
                        validModels.Add(821);
                        validModels.Add(881);
                        validModels.Add(1168);
                        validModels.Add(1167);
                        validModels.Add(1170);
                    }
                    break;
                case eRealm.Hibernia:
                    validModels.Add(1180);
                    if (Level > 10)
                        validModels.Add(1181);
                    if (Level > 20)
                        validModels.Add(1185);
                    if (Level > 30)
                        validModels.Add(1184);
                    if (Level > 40)
                    {
                        validModels.Add(1178);
                        validModels.Add(1174);
                        validModels.Add(1175);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(1179);
                        validModels.Add(1173);
                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(327);
                    if (Level > 10)
                        validModels.Add(565);
                    if (Level > 20)
                        validModels.Add(828);
                    if (Level > 30)
                        validModels.Add(1171);
                    if (Level > 40)
                    {
                        validModels.Add(1172);
                        validModels.Add(1176);
                    }
                    if (Level > 50)
                        validModels.Add(1177);
                    break;
                default:
                    validModels.Add(931);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3667);
            }
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3710);
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetShieldModelForLevel(int Level, eRealm realm, int size)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    switch (size)
                    {
                        case 1:
                            validModels.Add(1046);
                            validModels.Add(1047);
                            validModels.Add(1048);
                            if (Level > 10)
                            {
                                validModels.Add(1082);
                                validModels.Add(1083);
                                validModels.Add(1084);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1100);
                                validModels.Add(1101);
                                validModels.Add(1102);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1163);
                                validModels.Add(1164);
                                validModels.Add(1165);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3888);
                            break;
                        case 2:
                            validModels.Add(1055);
                            validModels.Add(1056);
                            validModels.Add(1057);
                            if (Level > 10)
                            {
                                validModels.Add(1091);
                                validModels.Add(1092);
                                validModels.Add(1093);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1145);
                                validModels.Add(1146);
                                validModels.Add(1147);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1148);
                                validModels.Add(1149);
                                validModels.Add(1150);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1160);
                                validModels.Add(1161);
                                validModels.Add(1162);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3889);
                            break;
                        case 3:
                            validModels.Add(1082);
                            validModels.Add(1083);
                            validModels.Add(1084);
                            if (Level > 10)
                            {
                                validModels.Add(1073);
                                validModels.Add(1074);
                                validModels.Add(1075);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1064);
                                validModels.Add(1065);
                                validModels.Add(1066);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1151);
                                validModels.Add(1152);
                                validModels.Add(1153);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1154);
                                validModels.Add(1155);
                                validModels.Add(1156);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3890);
                            break;
                    }
                    break;
                case eRealm.Albion:
                    switch (size)
                    {
                        case 1:
                            validModels.Add(1040);
                            validModels.Add(1041);
                            validModels.Add(1042);
                            if (Level > 10)
                            {
                                validModels.Add(1103);
                                validModels.Add(1104);
                                validModels.Add(1105);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1118);
                                validModels.Add(1119);
                                validModels.Add(1120);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3965);
                            break;
                        case 2:
                            validModels.Add(1094);
                            validModels.Add(1095);
                            validModels.Add(1096);
                            if (Level > 10)
                            {
                                validModels.Add(1049);
                                validModels.Add(1050);
                                validModels.Add(1051);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1085);
                                validModels.Add(1086);
                                validModels.Add(1087);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1106);
                                validModels.Add(1107);
                                validModels.Add(1108);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1115);
                                validModels.Add(1116);
                                validModels.Add(1117);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3966);
                            break;
                        case 3:
                            validModels.Add(1058);
                            validModels.Add(1059);
                            validModels.Add(1060);
                            if (Level > 10)
                            {
                                validModels.Add(1067);
                                validModels.Add(1068);
                                validModels.Add(1069);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1112);
                                validModels.Add(1113);
                                validModels.Add(1114);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1109);
                                validModels.Add(1110);
                                validModels.Add(1111);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1121);
                                validModels.Add(1122);
                                validModels.Add(1123);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3967);
                            break;
                    }
                    break;
                case eRealm.Midgard:
                    switch (size)
                    {
                        case 1:
                            validModels.Add(1043);
                            validModels.Add(1044);
                            validModels.Add(1045);
                            if (Level > 10)
                            {
                                validModels.Add(1124);
                                validModels.Add(1125);
                                validModels.Add(1126);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1139);
                                validModels.Add(1140);
                                validModels.Add(1141);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1130);
                                validModels.Add(1131);
                                validModels.Add(1132);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3929);
                            break;
                        case 2:
                            validModels.Add(1097);
                            validModels.Add(1098);
                            validModels.Add(1099);
                            if (Level > 10)
                            {
                                validModels.Add(1088);
                                validModels.Add(1089);
                                validModels.Add(1090);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1052);
                                validModels.Add(1053);
                                validModels.Add(1054);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1127);
                                validModels.Add(1128);
                                validModels.Add(1129);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3930);
                            break;
                        case 3:
                            validModels.Add(1079);
                            validModels.Add(1080);
                            validModels.Add(1081);
                            if (Level > 10)
                            {
                                validModels.Add(1061);
                                validModels.Add(1062);
                                validModels.Add(1063);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1133);
                                validModels.Add(1134);
                                validModels.Add(1135);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1136);
                                validModels.Add(1137);
                                validModels.Add(1138);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1142);
                                validModels.Add(1143);
                                validModels.Add(1144);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3931);
                            break;
                    }
                    break;
                default:
                    validModels.Add(59);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetInstrumentModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            validModels.Add(227);
            validModels.Add(228);
            validModels.Add(325);
            if (Level > 10)
            {
                validModels.Add(2974);
                validModels.Add(2975);
                validModels.Add(2973);
            }
            if (Level > 20)
            {
                validModels.Add(2970);
                validModels.Add(2971);
                validModels.Add(2972);
            }
            if (Level > 30)
            {
                if (realm == eRealm.Albion)
                {
                    validModels.Add(2976);
                    validModels.Add(2977);
                    validModels.Add(2978);
                }
                else if (realm == eRealm.Hibernia)
                {
                    validModels.Add(2979);
                    validModels.Add(2980);
                    validModels.Add(2981);
                }

            }
            if (Level > 40)
            {
                validModels.Add(2114);
                validModels.Add(2115);
                validModels.Add(2116);
                validModels.Add(2117);
            }
            if (Level > 50 && Util.Chance(1))
            {
                validModels.Add(3688);
                validModels.Add(3731);
                validModels.Add(3848);
                if (Util.Chance(50))
                {
                    if (realm == eRealm.Albion)
                        validModels.Add(3985);
                    if (realm == eRealm.Hibernia)
                        validModels.Add(3908);
                }
                if (Util.Chance(5))
                {
                    if (realm == eRealm.Albion)
                        validModels.Add(3280);
                    if (realm == eRealm.Hibernia)
                        validModels.Add(3239);
                }
            }
            return validModels[Util.Random(validModels.Count - 1)];
        }

        #endregion
    }
}