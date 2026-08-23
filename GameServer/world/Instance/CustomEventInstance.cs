using DOL.Database;
using DOL.GS.Scripts;
using DOL.GS.Styles;
using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static DOL.GS.GameObject;

namespace DOL.GS
{
    public enum eInstanceRule
    {
        None = 0,
        Solo = 1,
        Group = 2,
        Guild = 3,
        Battlegroup = 4
    }

    public class CustomEventInstance : RegionInstance
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        public string InstanceOwnerID { get; set; }
        public Group Group { get; set; }
        public Guild Guild { get; set; }
        public BattleGroup BattleGroup { get; set; }
        public GamePlayer Player { get; set; }

        public eInstanceRule InstanceType { get; set; } = eInstanceRule.Solo;

        private class OriginalMobState
        {
            public byte Level;
            public short Strength;
            public short Constitution;
            public short Dexterity;
            public short Quickness;
            public byte ParryChance;
            public byte EvadeChance;
            public byte BlockChance;
            public byte LeftHandSwingChance;
            public eDamageType MeleeDamageType;
            public int WeaponDps;
            public int WeaponSpd;
            public IList Spells;
            public List<Style> Styles;
            public IGameInventory Inventory;
            public bool IsBoss;
        }

        private Dictionary<GameNPC, OriginalMobState> m_originalMobStates = new Dictionary<GameNPC, OriginalMobState>();

        private static readonly bool[] IsBipedalModel = new bool[5000];

        private static readonly HashSet<string> ValidMobSpellTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Offensive & Debuffs
            "DirectDamage", "Lifedrain", "DirectDamageWithDebuff", "AcuityDebuff", "DexterityDebuff", "QuicknessDebuff",
            "ConstitutionDebuff", "MatterResistDebuff", "HeatResistDebuff", "ColdResistDebuff", "EnergyResistDebuff",
            "DexterityQuicknessDebuff", "StrengthConstitutionDebuff", "CombatSpeedDebuff", "ArmorAbsorptionDebuff",
            "ArmorFactorDebuff", "DamageOverTime", "MeleeDamageDebuff", "AllStatsPercentDebuff", "CrushSlashThrustDebuff",
            "WeaponSkillConstitutionDebuff", "EffectivenessDebuff", "FumbleChanceDebuff", "ToHitDebuff", "AllStatsDebuff",
            "FatigueConsumptionDebuff", "CastingSpeedDebuff", "Disease", "Stun", "Mez", "Taunt", "Slow", "Petrify", "Demi",
            "Quarter", "Earthquake", "Damnation", "DeathClaw", "CallAreaEffect", "BumpSpell", "OmniHarm", "Bolt", "SpeedDecrease",
            "DamageSpeedDecrease", "Nearsight",
            
            // Combat Spells & Procs
            "CombatHeal", "DamageAdd", "ArmorFactorBuff", "DexterityQuicknessBuff", "EnduranceRegenBuff",
            "CombatSpeedBuff", "AblativeArmor", "OffensiveProc", "DefensiveProc", "DamageShield", "BladeTurn",
            "BothAblativeArmor", "SpellReflection", "AllStatsBuff", "TriggerBuff", "TensionBuff",
            "SpellShield", "DebuffImmunity", "CriticalMagicalBuff", "CriticalMeleeBuff", "OffProcShear",
            "PowerShield", "MagicHealAbsorb", "DamageReturn",
            
            // Cures
            "CureDisease", "CurePoison", "CureNearsight", "CureMezz", "ArawnCure", "Unpetrify", "CureAll", "MaidenKiss",
            
            // Summons
            "Summon", "SummonMinion", "SummonCommander", "SummonDruidPet", "SummonHunterPet",
            "SummonNecroPet", "SummonMastery", "SummonMercenary", "SummonMonster", "SummonMultiTemplatePets",
            "SummonUnderhill", "SummonSimulacrum", "SummonSpiritFighter", "SummonTheurgistPet",
            
            // Heals
            "Heal", "HealOverTime", "MercHeal", "OmniHeal", "PBAEHeal", "SpreadHeal", "ZombieHeal",
            
            // Buffs
            "AcuityBuff", "AFHitsBuff", "AllMagicResistsBuff", "ArmorAbsorptionBuff", "BodyResistBuff",
            "BodySpiritEnergyBuff", "Buff", "CelerityBuff", "ColdResistBuff", "ConstitutionBuff", "CourageBuff",
            "CrushSlashThrustBuff", "DexterityBuff", "EffectivenessBuff", "EnergyResistBuff", "FatigueConsumptionBuff",
            "FelxibleSkillBuff", "HasteBuff", "HealthRegenBuff", "HeatColdMatterBuff", "HeatResistBuff", "HeroismBuff",
            "KeepDamageBuff", "MagicResistsBuff", "MatterResistBuff", "MeleeDamageBuff", "MesmerizeDurationBuff",
            "PaladinArmorFactorBuff", "ParryBuff", "PowerHealthEnduranceRegenBuff", "PowerRegenBuff",
            "SavageCombatSpeedBuff", "SavageCrushResistanceBuff", "SavageDPSBuff", "SavageParryBuff",
            "SavageSlashResistanceBuff", "SavageThrustResistanceBuff", "SpiritResistBuff", "StrengthBuff",
            "StrengthConstitutionBuff", "SuperiorCourageBuff", "ToHitBuff"
        };

        static CustomEventInstance()
        {
            ushort[] bipedalIds = new ushort[]
            {
                5, 6, 7, 8, 9, 10, 14, 16, 17, 18, 19, 20, 24, 25, 26, 27, 28, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 48, 49, 50, 51, 52, 53, 54, 55, 57, 58, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 106, 107, 108, 109, 110, 111, 113, 119, 120, 121, 122, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 230, 231, 232, 233, 234, 235, 236, 237, 238, 239, 240, 245, 246, 247, 248, 249, 250, 251, 252, 253, 254, 255, 256, 257, 258, 259, 260, 261, 262, 263, 264, 265, 266, 267, 268, 269, 270, 271, 272, 273, 274, 275, 276, 277, 278, 279, 280, 281, 282, 283, 284, 285, 286, 287, 288, 289, 290, 291, 292, 293, 294, 295, 296, 297, 298, 299, 300, 301, 302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313, 314, 315, 316, 317, 318, 319, 320, 321, 322, 323, 324, 325, 326, 327, 328, 329, 330, 331, 332, 333, 334, 335, 336, 337, 338, 339, 340, 341, 342, 343, 344, 345, 346, 347, 348, 349, 350, 351, 352, 353, 354, 355, 356, 357, 358, 359, 360, 361, 362, 363, 364, 365, 366, 367, 368, 369, 370, 371, 372, 373, 374, 375, 376, 377, 378, 379, 380, 381, 382, 383, 384, 385, 386, 387, 388, 389, 390, 391, 392, 393, 394, 395, 396, 397, 401, 403, 408, 409, 410, 411, 415, 416, 417, 418, 419, 420, 421, 422, 423, 424, 425, 426, 427, 428, 429, 430, 431, 432, 433, 434, 435, 436, 437, 438, 439, 442, 443, 444, 445, 446, 451, 452, 461, 462, 467, 468, 471, 472, 473, 474, 475, 476, 477, 478, 479, 480, 481, 482, 483, 484, 485, 486, 487, 488, 489, 490, 491, 492, 493, 494, 495, 496, 497, 498, 499, 500, 501, 502, 503, 504, 505, 506, 507, 508, 509, 510, 511, 512, 513, 514, 515, 516, 517, 518, 519, 520, 521, 522, 523, 524, 525, 526, 527, 528, 529, 530, 531, 532, 533, 534, 535, 536, 537, 538, 539, 540, 541, 542, 543, 544, 545, 546, 547, 548, 549, 550, 551, 552, 553, 554, 555, 556, 557, 558, 559, 560, 561, 562, 563, 564, 565, 566, 578, 579, 582, 583, 588, 589, 600, 602, 609, 610, 611, 615, 616, 617, 618, 620, 621, 622, 624, 625, 627, 628, 645, 646, 657, 661, 662, 663, 667, 671, 672, 673, 674, 675, 676, 677, 680, 681, 682, 683, 687, 700, 701, 702, 703, 704, 705, 706, 707, 708, 709, 710, 711, 712, 713, 714, 715, 716, 717, 718, 719, 720, 721, 722, 723, 724, 725, 726, 727, 728, 729, 730, 731, 732, 733, 734, 735, 736, 737, 738, 739, 740, 741, 742, 743, 744, 745, 746, 747, 748, 749, 750, 751, 752, 753, 754, 755, 756, 757, 758, 759, 760, 761, 762, 763, 767, 768, 772, 773, 774, 775, 776, 777, 778, 779, 780, 781, 782, 783, 784, 785, 786, 787, 788, 789, 790, 791, 792, 793, 794, 795, 796, 797, 798, 799, 800, 801, 802, 803, 804, 805, 806, 807, 808, 809, 810, 811, 812, 814, 815, 822, 825, 826, 827, 830, 832, 833, 834, 835, 836, 837, 838, 839, 840, 841, 845, 849, 850, 851, 852, 853, 854, 855, 856, 859, 861, 864, 865, 866, 867, 868, 869, 870, 871, 872, 873, 874, 875, 876, 877, 878, 879, 880, 881, 882, 883, 884, 885, 886, 887, 889, 890, 893, 894, 895, 896, 897, 898, 899, 900, 901, 917, 918, 919, 927, 945, 950, 952, 954, 955, 956, 957, 958, 959, 960, 961, 962, 963, 964, 965, 976, 977, 978, 979, 980, 981, 982, 984, 985, 986, 987, 988, 989, 990, 991, 992, 993, 994, 995, 996, 997, 998, 1008, 1009, 1010, 1011, 1012, 1013, 1014, 1015, 1016, 1017, 1018, 1019, 1020, 1021, 1022, 1023, 1024, 1025, 1026, 1027, 1028, 1029, 1030, 1031, 1032, 1033, 1034, 1035, 1036, 1037, 1038, 1039, 1040, 1041, 1042, 1043, 1044, 1045, 1046, 1047, 1048, 1050, 1051, 1052, 1053, 1054, 1055, 1056, 1057, 1058, 1059, 1060, 1061, 1062, 1063, 1064, 1065, 1066, 1067, 1068, 1069, 1070, 1071, 1072, 1073, 1074, 1075, 1076, 1077, 1078, 1079, 1080, 1081, 1082, 1083, 1084, 1085, 1086, 1087, 1088, 1089, 1090, 1091, 1092, 1093, 1094, 1095, 1096, 1097, 1098, 1099, 1100, 1101, 1102, 1103, 1104, 1105, 1106, 1107, 1108, 1109, 1110, 1111, 1112, 1113, 1114, 1115, 1116, 1117, 1118, 1119, 1120, 1121, 1122, 1123, 1124, 1125, 1126, 1127, 1128, 1129, 1130, 1131, 1132, 1133, 1134, 1135, 1136, 1137, 1138, 1139, 1140, 1141, 1142, 1143, 1144, 1145, 1146, 1147, 1148, 1149, 1150, 1151, 1152, 1153, 1154, 1155, 1156, 1157, 1158, 1159, 1160, 1161, 1162, 1163, 1164, 1165, 1166, 1167, 1168, 1169, 1170, 1181, 1182, 1183, 1184, 1185, 1186, 1187, 1188, 1189, 1190, 1191, 1192, 1203, 1204, 1209, 1213, 1214, 1215, 1216, 1217, 1218, 1220, 1221, 1222, 1223, 1224, 1225, 1226, 1227, 1228, 1229, 1230, 1231, 1255, 1265, 1267, 1268, 1270, 1271, 1272, 1273, 1274, 1277, 1278, 1279, 1280, 1281, 1282, 1283, 1284, 1285, 1286, 1287, 1288, 1289, 1290, 1291, 1292, 1293, 1294, 1295, 1296, 1297, 1298, 1299, 1300, 1301, 1302, 1303, 1304, 1305, 1306, 1307, 1308, 1309, 1310, 1311, 1312, 1313, 1314, 1315, 1316, 1317, 1318, 1319, 1320, 1321, 1322, 1323, 1324, 1325, 1326, 1327, 1328, 1329, 1330, 1331, 1332, 1333, 1334, 1335, 1336, 1337, 1338, 1339, 1340, 1341, 1342, 1343, 1344, 1345, 1346, 1347, 1348, 1351, 1352, 1353, 1354, 1355, 1356, 1357, 1358, 1359, 1360, 1361, 1362, 1363, 1364, 1365, 1366, 1367, 1368, 1369, 1370, 1371, 1372, 1373, 1374, 1375, 1376, 1377, 1378, 1379, 1380, 1381, 1382, 1383, 1384, 1385, 1386, 1387, 1388, 1389, 1390, 1395, 1396, 1397, 1398, 1399, 1400, 1401, 1402, 1403, 1404, 1405, 1406, 1407, 1408, 1409, 1410, 1411, 1412, 1413, 1414, 1415, 1416, 1417, 1418, 1419, 1420, 1421, 1422, 1423, 1424, 1425, 1426, 1427, 1428, 1429, 1430, 1431, 1432, 1433, 1434, 1435, 1436, 1437, 1439, 1440, 1563, 1564, 1565, 1566, 1574, 1575, 1576, 1577, 1578, 1579, 1580, 1581, 1582, 1585, 1586, 1623, 1625, 1642, 1647, 1648, 1649, 1650, 1651, 1652, 1653, 1654, 1655, 1656, 1657, 1658, 1659, 1660, 1661, 1662, 1663, 1664, 1665, 1666, 1667, 1668, 1669, 1670, 1673, 1688, 1689, 1690, 1691, 1698, 1699, 1700, 1701, 1705, 1706, 1707, 1708, 1709, 1710, 1711, 1712, 1713, 1714, 1715, 1716, 1717, 1718, 1719, 1720, 1721, 1722, 1723, 1724, 1725, 1726, 1727, 1728, 1729, 1730, 1731, 1732, 1733, 1734, 1735, 1736, 1740, 1741, 1742, 1743, 1744, 1749, 1750, 1751, 1752, 1753, 1754, 1755, 1756, 1757, 1758, 1759, 1760, 1770, 1773, 1774, 1775, 1776, 1777, 1778, 1779, 1780, 1781, 1782, 1783, 1784, 1785, 1786, 1787, 1788, 1789, 1790, 1791, 1792, 1793, 1794, 1795, 1796, 1797, 1798, 1799, 1800, 1801, 1802, 1803, 1804, 1805, 1806, 1807, 1808, 1809, 1810, 1811, 1812, 1813, 1814, 1815, 1816, 1817, 1818, 1819, 1820, 1821, 1824, 1825, 1826, 1827, 1828, 1829, 1830, 1831, 1832, 1833, 1834, 1835, 1836, 1837, 1838, 1839, 1840, 1841, 1842, 1843, 1844, 1845, 1846, 1847, 1848, 1849, 1850, 1851, 1852, 1853, 1854, 1855, 1856, 1857, 1858, 1859, 1860, 1861, 1864, 1883, 1884, 1885, 1888, 1889, 1890, 1891, 1892, 1893, 1894, 1895, 1896, 1897, 1898, 1899, 1900, 1901, 1902, 1903, 1904, 1911, 1912, 1913, 1914, 1915, 1916, 1917, 1918, 1919, 1920, 1921, 1922, 1929, 1930, 1931, 1932, 1935, 1938, 1944, 1945, 1946, 1947, 1948, 1949, 1950, 1951, 1952, 1953, 1954, 1955, 1960, 1961, 1962, 1963, 1964, 1965, 1966, 1967, 1968, 1969, 1970, 1971, 1972, 1973, 1974, 1975, 1976, 1977, 1978, 1979, 1980, 1981, 1982, 1983, 1984, 1985, 1986, 1987, 1988, 1989, 1990, 1991, 1992, 1993, 1994, 1995, 1996, 1997, 1998, 1999, 2013, 2014, 2015, 2016, 2017, 2018, 2022, 2023, 2024, 2025, 2032, 2036, 2045, 2046, 2060, 2061, 2062, 2063, 2064, 2065, 2066, 2076, 2077, 2078, 2079, 2080, 2081, 2082, 2083, 2084, 2085, 2086, 2087, 2088, 2089, 2090, 2091, 2092, 2093, 2094, 2095, 2096, 2097, 2098, 2099, 2100, 2101, 2102, 2103, 2104, 2105, 2106, 2107, 2108, 2109, 2110, 2111, 2112, 2113, 2114, 2115, 2116, 2118, 2119, 2120, 2121, 2122, 2123, 2124, 2125, 2126, 2133, 2134, 2136, 2137, 2138, 2139, 2140, 2141, 2142, 2143, 2144, 2145, 2146, 2147, 2148, 2149, 2151, 2152, 2154, 2155, 2156, 2162, 2163, 2165, 2166, 2168, 2169, 2170, 2175, 2176, 2177, 2185, 2186, 2187, 2188, 2189, 2190, 2191, 2192, 2193, 2194, 2195, 2196, 2197, 2198, 2199, 2200, 2201, 2202, 2203, 2204, 2205, 2206, 2207, 2208, 2209, 2210, 2211, 2212, 2213, 2214, 2215, 2216, 2217, 2218, 2220, 2221, 2222, 2244, 2245, 2246, 2247, 2249, 2253, 2267, 2310, 2311, 2312, 2313, 2314, 2315, 2316, 2317, 2347, 2348, 2349, 2350
            };

            foreach (ushort id in bipedalIds)
            {
                if (id < IsBipedalModel.Length)
                    IsBipedalModel[id] = true;
            }
        }

        public CustomEventInstance(ushort ID, GameTimer.TimeManager time, RegionData dat)
            : base(ID, time, dat)
        {
        }

        /// <summary>
        /// Save the original state of a mob before any modification
        /// </summary>
        private void SaveOriginalState(GameNPC mob)
        {
            if (!m_originalMobStates.ContainsKey(mob))
            {
                m_originalMobStates[mob] = new OriginalMobState
                {
                    Level = mob.Level,
                    Strength = mob.Strength,
                    Constitution = mob.Constitution,
                    Dexterity = mob.Dexterity,
                    Quickness = mob.Quickness,
                    ParryChance = mob.ParryChance,
                    EvadeChance = mob.EvadeChance,
                    BlockChance = mob.BlockChance,
                    LeftHandSwingChance = mob.LeftHandSwingChance,
                    MeleeDamageType = mob.MeleeDamageType,
                    WeaponDps = mob.WeaponDps,
                    WeaponSpd = mob.WeaponSpd,
                    Spells = mob.Spells,
                    Styles = mob.Styles,
                    Inventory = mob.Inventory,
                    IsBoss = mob.IsBoss
                };
            }
        }

        /// <summary>
        /// Restores mobs completely to their original database status
        /// </summary>
        public void RevertMobModifications()
        {
            if (m_originalMobStates.Count == 0) return;

            foreach (var kvp in m_originalMobStates)
            {
                GameNPC mob = kvp.Key;
                OriginalMobState orig = kvp.Value;

                if (mob != null && mob.ObjectState == eObjectState.Active && mob.IsAlive)
                {
                    mob.TempProperties.removeProperty(PlayerCloner.CLONE_CLASS_NAME);
                    mob.Spells = orig.Spells;
                    mob.Styles = orig.Styles;
                    mob.ParryChance = orig.ParryChance;
                    mob.EvadeChance = orig.EvadeChance;
                    mob.BlockChance = orig.BlockChance;
                    mob.LeftHandSwingChance = orig.LeftHandSwingChance;
                    mob.Inventory = orig.Inventory;
                    mob.MeleeDamageType = orig.MeleeDamageType;
                    mob.IsBoss = orig.IsBoss;
                    mob.Level = orig.Level;

                    mob.AutoSetStats();
                    mob.Strength = orig.Strength;
                    mob.Constitution = orig.Constitution;
                    mob.Dexterity = orig.Dexterity;
                    mob.Quickness = orig.Quickness;
                    mob.WeaponDps = orig.WeaponDps;
                    mob.WeaponSpd = orig.WeaponSpd;

                    mob.UpdateMaxHealth();
                    mob.Health = mob.MaxHealth;

                    if (mob.Inventory != null)
                    {
                        if (mob.Inventory.GetItem(eInventorySlot.TwoHandWeapon) != null)
                            mob.SwitchWeapon(GameLiving.eActiveWeaponSlot.TwoHanded);
                        else if (mob.Inventory.GetItem(eInventorySlot.DistanceWeapon) != null)
                            mob.SwitchWeapon(GameLiving.eActiveWeaponSlot.Distance);
                        else
                            mob.SwitchWeapon(GameLiving.eActiveWeaponSlot.Standard);
                    }

                    mob.BroadcastLivingEquipmentUpdate();
                }
            }
            m_originalMobStates.Clear();
        }

        /// <summary>
        /// Scales all the mobs inside the instance to the target level, applying Boss Modifiers.
        /// </summary>
        public void ScaleMobs(int targetLevel, string bossScaling)
        {
            Dictionary<string, int> bosses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(bossScaling))
            {
                var splits = bossScaling.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var split in splits)
                {
                    var parts = split.Split('|');
                    if (parts.Length == 2 && int.TryParse(parts[1].Replace("+", ""), out int diff))
                    {
                        bosses[parts[0].Trim()] = diff;
                    }
                }
            }

            // Calculate the base target level with the offset included
            int baseTargetLevel = Math.Max(1, Math.Min(255, targetLevel));

            foreach (GameNPC mob in GetMobsInsideInstance(false))
            {
                SaveOriginalState(mob);

                if (bosses.TryGetValue(mob.Name, out int diff))
                {
                    mob.Level = (byte)Math.Max(1, Math.Min(255, baseTargetLevel + diff));
                    mob.IsBoss = true;
                }
                else
                {
                    mob.Level = (byte)baseTargetLevel;
                }

                mob.AutoSetStats();
                mob.Health = mob.MaxHealth;
            }
        }

        public void ApplyPlayerClassesToMobs(List<GamePlayer> players, ushort equipColor)
        {
            if (players == null || players.Count == 0) return;

            List<GameNPC> mobs = new List<GameNPC>();
            foreach (GameNPC m in GetMobsInsideInstance(false))
            {
                if (m.BodyType == (int)NpcTemplateMgr.eBodyType.Humanoid ||
                   (m.Model < IsBipedalModel.Length && IsBipedalModel[m.Model]))
                {
                    mobs.Add(m);
                }
            }

            if (mobs.Count == 0) return;

            // Fisher-Yates array shuffle ensures an exact even spread of group classes without CPU overhead
            Random rnd = new Random();
            for (int i = mobs.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                GameNPC temp = mobs[i];
                mobs[i] = mobs[j];
                mobs[j] = temp;
            }

            int playerIndex = 0;
            int[] masteryDefenseBonus = { 0, 2, 5, 10, 16, 23 };

            foreach (GameNPC mob in mobs)
            {
                SaveOriginalState(mob);

                GamePlayer playerToMimic = players[playerIndex % players.Count];
                playerIndex++;

                eCharacterClass cClass = playerToMimic.CharacterClass != null ? (eCharacterClass)playerToMimic.CharacterClass.ID : eCharacterClass.Unknown;
                if (cClass == eCharacterClass.Unknown) continue;

                mob.TempProperties.setProperty(PlayerCloner.CLONE_CLASS_NAME, playerToMimic.CharacterClass!.Name);
                mob.Styles = PlayerCloner.GetStyles(playerToMimic);

                IList rawSpells = PlayerCloner.GetSpells(playerToMimic);
                ArrayList filteredSpells = new ArrayList();
                if (rawSpells != null)
                {
                    foreach (Spell s in rawSpells)
                    {
                        if (s != null && ValidMobSpellTypes.Contains(s.SpellType))
                        {
                            filteredSpells.Add(s);
                        }
                    }
                }
                mob.Spells = filteredSpells;

                byte parryChance = (byte)playerToMimic.GetParryChance();
                if (parryChance == 0 && playerToMimic.GetModifiedSpecLevel(Specs.Parry) > 0)
                {
                    int spec = playerToMimic.GetModifiedSpecLevel(Specs.Parry);
                    int mastery = playerToMimic.GetAbilityLevel("Mastery of Parrying");
                    int masteryBonus = mastery > 0 && mastery <= 5 ? masteryDefenseBonus[mastery] : 0;
                    parryChance = (byte)(33.0 * ((spec * 0.0151) + (masteryBonus * 0.01)));
                }
                mob.ParryChance = parryChance;

                byte evadeChance = (byte)playerToMimic.GetEvadeChance();
                if (evadeChance == 0 && playerToMimic.HasAbility(Abilities.Evade))
                {
                    int evadeLevel = playerToMimic.GetAbilityLevel(Abilities.Evade);
                    evadeChance = (byte)(32.0 * evadeLevel / 7.0);
                }
                mob.EvadeChance = evadeChance;

                mob.Strength = (short)playerToMimic.GetModified(eProperty.Strength);
                mob.Constitution = (short)playerToMimic.GetModified(eProperty.Constitution);
                mob.Dexterity = (short)playerToMimic.GetModified(eProperty.Dexterity);
                mob.Quickness = (short)(100 + mob.Level);

                GameNpcInventoryTemplate template = new GameNpcInventoryTemplate();
                int level = mob.Level > 0 ? mob.Level : 50;

                eRealm npcRealm = mob.Realm == eRealm.None ? eRealm.Albion : mob.Realm;
                eObjectType armorType = ItemModelManager.GetDefaultArmorMaterialForClass(cClass, level);
                eInventorySlot[] armorSlots = { eInventorySlot.HeadArmor, eInventorySlot.HandsArmor, eInventorySlot.FeetArmor, eInventorySlot.TorsoArmor, eInventorySlot.LegsArmor, eInventorySlot.ArmsArmor };

                foreach (eInventorySlot slot in armorSlots)
                {
                    ItemModelManager.GetArmorData(armorType, slot, level, npcRealm, "None", mob.Model, out int model, out string name, out bool ext, out int headEffect);
                    if (model > 0)
                    {
                        template.AddNPCEquipment(slot, model, equipColor, headEffect, ext ? 5 : 0);
                    }
                }

                int cloakChance = level < 20 ? 20 : (level <= 42 ? 50 : 75);
                if (Util.Chance(cloakChance))
                {
                    int cloakModel = ItemModelManager.GetCloakModel("class", cClass);
                    if (cloakModel <= 0) cloakModel = ItemModelManager.GetCloakModel("Regular");
                    template.AddNPCEquipment(eInventorySlot.Cloak, cloakModel, equipColor, 0, 0);
                }

                ItemModelManager.EquipClassWeapons(mob, template, cClass, "classic", equipColor);
                mob.Inventory = template.CloseTemplate();

                InventoryItem leftWeapon = mob.Inventory.GetItem(eInventorySlot.LeftHandWeapon);
                if (leftWeapon != null && leftWeapon.Object_Type == (int)eObjectType.Shield)
                {
                    byte blockChance = (byte)playerToMimic.GetBlockChance();
                    if (blockChance == 0 && playerToMimic.GetModifiedSpecLevel(Specs.Shields) > 0)
                    {
                        int spec = playerToMimic.GetModifiedSpecLevel(Specs.Shields);
                        int mastery = playerToMimic.GetAbilityLevel("Mastery of Blocking");
                        int masteryBonus = mastery > 0 && mastery <= 5 ? masteryDefenseBonus[mastery] : 0;
                        blockChance = (byte)(35.0 * ((spec * 0.0151) + (masteryBonus * 0.01)));
                    }
                    mob.BlockChance = blockChance;
                    mob.LeftHandSwingChance = 0;
                }
                else if (playerToMimic.CanUseLefthandedWeapon && leftWeapon != null && leftWeapon.Object_Type != (int)eObjectType.Shield)
                {
                    mob.LeftHandSwingChance = 75;
                    mob.BlockChance = 0;
                }
                else
                {
                    mob.LeftHandSwingChance = 0;
                    mob.BlockChance = 0;
                }

                InventoryItem attackWeapon = mob.Inventory.GetItem(eInventorySlot.TwoHandWeapon) ?? mob.Inventory.GetItem(eInventorySlot.RightHandWeapon);
                if (attackWeapon != null)
                {
                    mob.MeleeDamageType = (eDamageType)attackWeapon.Type_Damage;
                }
                else
                {
                    mob.MeleeDamageType = eDamageType.Slash;
                }

                if (playerToMimic.AttackWeapon != null)
                {
                    mob.WeaponDps = (int)playerToMimic.WeaponDamage(playerToMimic.AttackWeapon);
                    mob.WeaponSpd = playerToMimic.AttackWeapon.SPD_ABS;
                }

                mob.UpdateMaxHealth();
                mob.Health = mob.MaxHealth;

                if (mob.Inventory.GetItem(eInventorySlot.TwoHandWeapon) != null)
                {
                    mob.SwitchWeapon(GameLiving.eActiveWeaponSlot.TwoHanded);
                }
                else if (mob.Inventory.GetItem(eInventorySlot.DistanceWeapon) != null)
                {
                    mob.SwitchWeapon(GameLiving.eActiveWeaponSlot.Distance);
                }
                else
                {
                    mob.SwitchWeapon(GameLiving.eActiveWeaponSlot.Standard);
                }

                mob.BroadcastLivingEquipmentUpdate();
            }
        }

        public void UpdateInstanceOwner()
        {
            if (this.NumPlayers < 1) return;

            bool stillOwner = false;

            if (InstanceType == eInstanceRule.Group && Group != null)
            {
                foreach (GamePlayer p in this.PlayersInside)
                    if (p.Group != null && p.Group == Group) { Player = Group.Leader; stillOwner = true; break; }
            }
            else if (InstanceType == eInstanceRule.Guild && Guild != null)
            {
                foreach (GamePlayer p in this.PlayersInside)
                    if (p.Guild != null && p.Guild == Guild) { Player = p; stillOwner = true; break; }
            }
            else if (InstanceType == eInstanceRule.Battlegroup && BattleGroup != null)
            {
                foreach (GamePlayer p in this.PlayersInside)
                    if (p.BattleGroup != null && p.BattleGroup == BattleGroup)
                    {
                        Player = p.BattleGroup.Leader;
                        stillOwner = true;
                        break;
                    }
            }
            else if (InstanceType == eInstanceRule.Solo && Player != null)
            {
                foreach (GamePlayer p in this.PlayersInside)
                    if (p == Player) { stillOwner = true; break; }
            }

            if (!stillOwner)
            {
                GamePlayer remaining = this.PlayersInside.FirstOrDefault();
                if (remaining != null)
                {
                    Player = remaining;
                    Group = remaining.Group;
                    Guild = remaining.Guild;
                    BattleGroup = remaining.BattleGroup;
                }
            }
        }

        public override void LoadFromDatabase(Mob[] mobObjs, ref long mobCount, ref long merchantCount, ref long itemCount, ref long bindCount)
        {
            base.LoadFromDatabase(mobObjs, ref mobCount, ref merchantCount, ref itemCount, ref bindCount);
            foreach (GameNPC mob in GetMobsInsideInstance(false))
            {
                mob.RespawnInterval = -1;
            }
        }

        public override void OnPlayerLeaveInstance(GamePlayer player)
        {
            base.OnPlayerLeaveInstance(player);

            if (this.NumPlayers > 0)
            {
                UpdateInstanceOwner();
            }
            else
            {
                foreach (GameNPC mob in GetMobsInsideInstance(true))
                {
                    log.Warn("Instance now empty, will destroy instance " + Description + ", ID: " + ID + " in " + ServerProperties.Properties.ADVENTUREWING_TIME_TO_DESTROY + " min.");
                    this.BeginAutoClosureCountdown(ServerProperties.Properties.ADVENTUREWING_TIME_TO_DESTROY);
                    return;
                }

                log.Warn("Instance now empty, will destroy instance " + Description + ", ID: " + ID + " Now!");
                WorldMgr.RemoveInstance(this);
            }
        }
    }
}