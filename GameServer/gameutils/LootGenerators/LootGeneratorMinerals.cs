using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;
using DOL.GS.Utils;
using log4net;

namespace DOL.GS
{
    public enum eCreatureCategory
    {
        None,
        Basic,
        Beast,
        Reptile,
        Bug,
        Undead,
        Magic,
        Water
    }

    /// <summary>
    /// Smart Mineral Loot Generator
    /// Dynamically drops minerals based on Mob Level, BodyType, Creature Category, Region Environment, and Region Category.
    /// </summary>
    public class LootGeneratorMinerals : LootGeneratorBase
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        // Cache for the ItemTemplates from the database
        private static readonly Dictionary<string, ItemTemplate> _mineralTemplates = new Dictionary<string, ItemTemplate>();

        // Fast O(1) dictionary parsing exact IDs to their targeted creature categories
        private static readonly Dictionary<ushort, eCreatureCategory> _modelToCreatureCategory = new Dictionary<ushort, eCreatureCategory>();

        // Internal struct to categorize each mineral
        private class MineralDef
        {
            public string Id_nb { get; set; }
            public int Tier { get; set; }
            public List<eRegionEnvironment> FavoredEnvironments { get; set; }
            public List<NpcTemplateMgr.eBodyType> FavoredBodyTypes { get; set; }
            public List<eCreatureCategory> FavoredCreatureCategories { get; set; }
        }

        private static readonly List<MineralDef> _mineralDefinitions = new List<MineralDef>();

        private static bool _dbLoaded = false;
        private static readonly object _dbLock = new object();

        static LootGeneratorMinerals()
        {
            // Group model IDs directly into overarching categories 
            var categoryDict = new Dictionary<eCreatureCategory, ushort[]>
            {
                { eCreatureCategory.Basic, new ushort[] {
                    5, 6, 7, 8, 9, 10, 14, 16, 17, 18, 19, 20, 27, 28, 32, 33, 34, 35, 36, 37, 38, 39, 40,
                    41, 42, 43, 44, 45, 46, 48, 49, 50, 51, 52, 53, 54, 55, 57, 58, 61, 62, 63, 64, 65, 66,
                    67, 68, 69, 70, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90,
                    91, 92, 93, 94, 95, 113, 119, 120, 121, 122, 137, 138, 139, 140, 141, 142, 143, 144, 145,
                    146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163,
                    164, 165, 166, 167, 168, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181,
                    182, 183, 184, 185, 186, 187, 188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199,
                    200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216, 217,
                    218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 230, 231, 232, 233, 234, 235,
                    236, 237, 238, 239, 240, 245, 246, 247, 248, 249, 250, 251, 252, 253, 254, 255, 256, 257,
                    258, 259, 260, 261, 262, 263, 264, 265, 266, 267, 268, 269, 270, 271, 272, 273, 274, 275,
                    276, 277, 278, 279, 280, 281, 282, 283, 284, 285, 286, 287, 288, 289, 290, 291, 292, 293,
                    294, 295, 296, 297, 298, 299, 300, 301, 302, 303, 304, 305, 306, 307, 308, 309, 310, 311,
                    312, 313, 314, 315, 316, 317, 318, 319, 320, 321, 322, 323, 324, 325, 326, 327, 328, 329,
                    330, 331, 332, 333, 334, 335, 336, 337, 338, 339, 340, 341, 342, 343, 344, 345, 346, 347,
                    348, 349, 350, 351, 352, 353, 354, 355, 356, 357, 358, 359, 360, 361, 362, 363, 364, 365,
                    366, 367, 368, 369, 370, 371, 372, 373, 374, 375, 376, 377, 378, 379, 380, 381, 382, 383,
                    384, 385, 386, 387, 388, 389, 390, 391, 392, 393, 394, 401, 402, 403, 408, 409, 410, 411,
                    412, 415, 416, 417, 418, 419, 420, 421, 422, 423, 424, 425, 426, 427, 428, 429, 430, 431,
                    432, 433, 434, 435, 436, 437, 438, 439, 460, 461, 471, 472, 473, 474, 475, 476, 477, 478,
                    479, 480, 481, 482, 483, 484, 485, 486, 487, 488, 489, 490, 491, 492, 493, 494, 495, 496,
                    497, 498, 499, 500, 501, 502, 503, 504, 505, 506, 507, 508, 509, 510, 511, 512, 513, 514,
                    515, 516, 517, 518, 519, 520, 521, 522, 523, 524, 525, 526, 527, 528, 529, 530, 531, 532,
                    533, 534, 535, 536, 537, 538, 539, 540, 541, 542, 543, 544, 545, 546, 547, 548, 549, 550,
                    551, 552, 553, 554, 555, 556, 557, 558, 559, 560, 561, 562, 563, 564, 565, 566, 578, 579,
                    582, 583, 588, 589, 600, 601, 602, 609, 610, 611, 615, 616, 618, 620, 621, 622, 627, 628,
                    643, 667, 671, 672, 673, 674, 675, 676, 677, 687, 700, 701, 702, 703, 704, 705, 706, 707,
                    708, 709, 710, 711, 712, 713, 714, 715, 716, 717, 718, 719, 720, 721, 722, 723, 724, 725,
                    726, 727, 728, 729, 730, 731, 732, 733, 734, 735, 736, 737, 738, 739, 740, 741, 742, 743,
                    744, 745, 746, 747, 748, 749, 750, 751, 752, 753, 754, 755, 756, 757, 758, 759, 760, 761,
                    762, 763, 767, 768, 772, 773, 774, 775, 776, 777, 778, 779, 780, 781, 782, 783, 784, 785,
                    786, 787, 788, 789, 790, 791, 792, 793, 794, 795, 796, 797, 798, 799, 800, 801, 802, 803,
                    804, 805, 806, 807, 808, 809, 810, 811, 812, 814, 815, 825, 826, 827, 830, 832, 833, 834,
                    835, 836, 837, 838, 839, 840, 841, 845, 849, 850, 851, 852, 853, 854, 855, 856, 859, 861,
                    864, 865, 866, 867, 868, 869, 870, 871, 872, 873, 874, 875, 917, 918, 919, 945, 950, 954,
                    955, 956, 957, 958, 959, 960, 961, 962, 963, 964, 965, 976, 977, 978, 979, 980, 981, 982,
                    984, 985, 986, 987, 988, 989, 990, 991, 992, 993, 994, 995, 996, 997, 998, 1008, 1009, 1010,
                    1011, 1012, 1013, 1014, 1015, 1016, 1017, 1018, 1019, 1020, 1021, 1022, 1023, 1024, 1025,
                    1026, 1027, 1028, 1029, 1030, 1031, 1032, 1033, 1034, 1035, 1036, 1037, 1038, 1039, 1040,
                    1041, 1042, 1043, 1044, 1045, 1046, 1047, 1048, 1050, 1051, 1052, 1053, 1054, 1055, 1056,
                    1057, 1058, 1059, 1060, 1061, 1062, 1063, 1064, 1065, 1066, 1067, 1068, 1069, 1070, 1071,
                    1072, 1073, 1074, 1075, 1076, 1077, 1078, 1079, 1080, 1081, 1082, 1083, 1084, 1085, 1086,
                    1087, 1088, 1089, 1090, 1091, 1092, 1093, 1094, 1095, 1096, 1097, 1098, 1099, 1100, 1101,
                    1102, 1103, 1104, 1105, 1106, 1107, 1108, 1109, 1110, 1111, 1112, 1113, 1114, 1115, 1116,
                    1117, 1118, 1119, 1120, 1121, 1122, 1123, 1124, 1125, 1126, 1127, 1128, 1129, 1130, 1131,
                    1132, 1133, 1134, 1135, 1136, 1137, 1138, 1139, 1140, 1141, 1142, 1143, 1144, 1145, 1146,
                    1147, 1148, 1149, 1150, 1151, 1152, 1153, 1154, 1155, 1156, 1157, 1158, 1159, 1160, 1161,
                    1162, 1163, 1164, 1165, 1166, 1167, 1168, 1169, 1170, 1190, 1191, 1192, 1203, 1204, 1210,
                    1213, 1214, 1215, 1216, 1217, 1218, 1220, 1221, 1222, 1223, 1224, 1225, 1226, 1227, 1228,
                    1229, 1230, 1231, 1255, 1265, 1267, 1268, 1270, 1271, 1272, 1273, 1274, 1313, 1314, 1315,
                    1316, 1317, 1318, 1319, 1320, 1321, 1322, 1323, 1324, 1325, 1326, 1327, 1328, 1329, 1330,
                    1331, 1332, 1333, 1334, 1335, 1336, 1337, 1338, 1339, 1340, 1341, 1342, 1343, 1344, 1345,
                    1346, 1347, 1348, 1395, 1396, 1397, 1398, 1399, 1400, 1401, 1402, 1403, 1404, 1405, 1406,
                    1407, 1408, 1409, 1410, 1411, 1412, 1413, 1414, 1415, 1416, 1417, 1418, 1419, 1420, 1421,
                    1422, 1423, 1424, 1425, 1426, 1427, 1428, 1429, 1430, 1431, 1432, 1433, 1434, 1435, 1436,
                    1437, 1439, 1440, 1563, 1564, 1565, 1566, 1574, 1577, 1580, 1625, 1642, 1647, 1648, 1649,
                    1650, 1651, 1652, 1653, 1654, 1655, 1656, 1657, 1658, 1659, 1660, 1661, 1662, 1663, 1664,
                    1665, 1666, 1667, 1668, 1669, 1688, 1689, 1698, 1699, 1700, 1701, 1705, 1706, 1707, 1708,
                    1709, 1710, 1711, 1712, 1713, 1714, 1715, 1716, 1717, 1718, 1719, 1720, 1721, 1722, 1723,
                    1724, 1725, 1726, 1727, 1728, 1729, 1730, 1731, 1732, 1733, 1734, 1735, 1736, 1740, 1741,
                    1742, 1743, 1744, 1749, 1750, 1751, 1752, 1753, 1754, 1755, 1756, 1757, 1758, 1759, 1760,
                    1770, 1773, 1774, 1775, 1776, 1777, 1778, 1779, 1780, 1781, 1782, 1783, 1784, 1785, 1786,
                    1787, 1788, 1789, 1790, 1791, 1792, 1793, 1794, 1795, 1796, 1797, 1798, 1799, 1800, 1801,
                    1802, 1803, 1804, 1805, 1806, 1807, 1808, 1809, 1810, 1811, 1812, 1813, 1814, 1815, 1816,
                    1817, 1818, 1819, 1820, 1821, 1824, 1825, 1826, 1864, 1894, 1895, 1896, 1897, 1898, 1899,
                    1900, 1901, 1902, 1903, 1904, 1911, 1912, 1913, 1914, 1915, 1916, 1917, 1918, 1919, 1920,
                    1921, 1922, 1930, 1931, 1932, 1935, 1944, 1945, 1946, 1947, 1948, 1949, 1950, 1951, 1952,
                    1953, 1954, 1955, 1960, 1961, 1962, 1963, 1964, 1965, 1966, 1967, 1968, 1969, 1970, 1971,
                    1972, 1973, 1974, 1975, 1976, 1977, 1978, 1979, 1980, 1981, 1982, 1983, 1984, 1985, 1986,
                    1987, 1988, 1989, 1990, 1991, 1992, 1993, 2013, 2014, 2015, 2016, 2017, 2018, 2022, 2023,
                    2024, 2025, 2032, 2060, 2061, 2062, 2063, 2064, 2065, 2066, 2076, 2077, 2110, 2111, 2112,
                    2113, 2114, 2115, 2116, 2119, 2120, 2122, 2123, 2124, 2125, 2136, 2137, 2138, 2139, 2140,
                    2141, 2142, 2143, 2144, 2145, 2146, 2147, 2148, 2149, 2154, 2155, 2165, 2169, 2170, 2185,
                    2211, 2212, 2244, 2245, 2246, 2247, 2267, 2310, 2311, 2312, 2313, 2314, 2315, 2316, 2317,
                    2318, 2326, 2328, 2347, 2348, 2349, 2350
                } },
                { eCreatureCategory.Beast, new ushort[] {
                    47, 56, 96, 99, 100, 101, 102, 103, 104, 133, 134, 135, 395, 396, 397, 413, 414, 447, 448,
                    449, 450, 459, 462, 463, 464, 465, 567, 568, 569, 572, 580, 604, 607, 608, 617, 624, 625,
                    626, 647, 648, 649, 650, 651, 657, 661, 662, 663, 688, 689, 690, 691, 692, 693, 694, 695,
                    770, 828, 843, 846, 888, 892, 924, 939, 949, 1193, 1208, 1256, 1596, 1673, 1747, 1869, 1870,
                    1886, 1956, 2001, 2002, 2003, 2027, 2028, 2029, 2030, 2031, 2033, 2034, 2035, 2036, 2037,
                    2044, 2047, 2056, 2057, 2127, 2128, 2129, 2130, 2131, 2132, 2153, 2157, 2159, 2160, 2161,
                    2164, 2167, 2172, 2173, 2174, 2179, 2180, 2181, 2182, 2183, 2184, 2223, 2224, 2225, 2226,
                    2227, 2228, 2229, 2230, 2231, 2232, 2233, 2234, 2235, 2236, 2237, 2257, 2287, 2288, 2289,
                    2290, 2327, 2346, 2351, 2352, 2353, 2354, 2356, 2360
                } },
                { eCreatureCategory.Reptile, new ushort[] {
                    29, 30, 31, 105, 398, 399, 400, 455, 456, 576, 597, 612, 613, 614, 765, 766, 829, 858, 863,
                    876, 877, 878, 879, 880, 881, 882, 883, 884, 885, 886, 887, 891, 893, 894, 895, 896, 897,
                    898, 899, 900, 901, 925, 926, 1182, 1183, 1184, 1185, 1186, 1187, 1188, 1189, 1209, 1233,
                    1234, 1235, 1258, 1259, 1260, 1261, 1266, 1687, 1933, 2162, 2163, 2243, 2274, 2276, 2277,
                    2280, 2281, 2282, 2283, 2296, 2297, 2298, 2308, 2309, 2320, 2321, 2322, 2323, 2324, 2325,
                    2329, 2330, 2331, 2333, 2334, 2335, 2336, 2337, 2338, 2340, 2341, 2342
                } },
                { eCreatureCategory.Undead, new ushort[] {
                    22, 23, 24, 25, 26, 106, 107, 108, 109, 110, 111, 440, 441, 442, 443, 444, 445, 446, 451,
                    452, 467, 468, 619, 655, 656, 659, 660, 680, 681, 682, 683, 817, 822, 889, 890, 902, 907,
                    908, 909, 910, 911, 912, 914, 916, 920, 921, 922, 923, 927, 929, 938, 948, 952, 1211, 1277,
                    1278, 1279, 1280, 1281, 1282, 1283, 1284, 1285, 1286, 1287, 1288, 1289, 1290, 1291, 1292,
                    1293, 1294, 1295, 1296, 1297, 1298, 1299, 1300, 1301, 1302, 1303, 1304, 1305, 1306, 1307,
                    1308, 1309, 1310, 1311, 1312, 1351, 1352, 1353, 1354, 1355, 1356, 1357, 1358, 1359, 1360,
                    1361, 1362, 1363, 1364, 1365, 1366, 1367, 1368, 1369, 1370, 1371, 1372, 1373, 1374, 1375,
                    1376, 1377, 1378, 1379, 1380, 1381, 1382, 1383, 1384, 1385, 1386, 1387, 1388, 1389, 1390,
                    1575, 1576, 1578, 1579, 1581, 1582, 1670, 1745, 1746, 1771, 1772, 1827, 1828, 1829, 1830,
                    1831, 1832, 1833, 1834, 1835, 1836, 1837, 1838, 1839, 1840, 1841, 1842, 1843, 1844, 1845,
                    1846, 1847, 1848, 1849, 1850, 1851, 1852, 1853, 1854, 1855, 1856, 1857, 1858, 1859, 1860,
                    1861, 1883, 1884, 1885, 1888, 1889, 1890, 1891, 1892, 1893, 1929, 1938, 1994, 1995, 1996,
                    1997, 1998, 1999, 2045, 2046, 2075, 2078, 2079, 2080, 2081, 2082, 2083, 2084, 2085, 2086,
                    2087, 2088, 2089, 2090, 2091, 2092, 2093, 2094, 2095, 2096, 2097, 2098, 2099, 2100, 2101,
                    2102, 2103, 2104, 2105, 2106, 2107, 2108, 2109, 2133, 2135, 2176, 2177, 2178, 2186, 2213,
                    2215, 2216, 2217, 2218, 2219, 2220, 2221, 2222, 2249, 2253, 2262, 2273, 2343, 2344, 2345
                } },
                { eCreatureCategory.Magic, new ushort[] {
                    124, 125, 126, 585, 586, 605, 606, 629, 634, 635, 636, 637, 638, 639, 642, 644, 645, 646,
                    652, 653, 697, 913, 928, 930, 931, 932, 933, 934, 935, 936, 937, 940, 941, 942, 943, 944,
                    1049, 1194, 1195, 1196, 1197, 1198, 1199, 1269, 1275, 1276, 1349, 2000, 2040, 2041, 2042,
                    2043, 2049, 2054, 2055, 2118, 2121, 2126, 2134, 2151, 2152, 2156, 2166, 2168, 2175, 2187,
                    2188, 2189, 2190, 2191, 2192, 2193, 2194, 2195, 2196, 2197, 2198, 2199, 2200, 2201, 2202,
                    2203, 2204, 2205, 2206, 2207, 2208, 2209, 2210, 2214, 2248, 2259, 2263, 2264, 2271, 2272,
                    2278, 2301, 2332, 2339
                } },
                { eCreatureCategory.Water, new ushort[] {
                    118, 574, 581, 594, 816, 823, 967, 968, 969, 970, 971, 972, 974, 975, 983, 999, 1000, 1001,
                    1002, 1003, 1004, 1005, 1006
                } }
            };

            foreach (var pair in categoryDict)
            {
                foreach (var id in pair.Value)
                {
                    _modelToCreatureCategory[id] = pair.Key;
                }
            }

            InitializeMineralDefinitions();
        }

        public LootGeneratorMinerals() : base()
        {
            // Thread-safe one-time DB load
            if (!_dbLoaded)
            {
                lock (_dbLock)
                {
                    if (!_dbLoaded)
                    {
                        LoadTemplatesFromDatabase();
                        _dbLoaded = true;
                    }
                }
            }
        }

        /// <summary>
        /// Determine the base chance of getting any drop at all based on BodyType
        /// </summary>
        private int GetBaseDropChance(NpcTemplateMgr.eBodyType bodyType)
        {
            switch (bodyType)
            {
                case NpcTemplateMgr.eBodyType.Dragon:
                case NpcTemplateMgr.eBodyType.Giant:
                    return 25; // Dragons and Giants have +5% over normal

                case NpcTemplateMgr.eBodyType.Animal:
                case NpcTemplateMgr.eBodyType.Insect:
                    return 15; // Animals and Insects have -5% from normal

                case NpcTemplateMgr.eBodyType.Magical:
                case NpcTemplateMgr.eBodyType.Plant:
                    return 4;  // Almost zero chance for Plants and Magical 

                case NpcTemplateMgr.eBodyType.Demon:
                case NpcTemplateMgr.eBodyType.Humanoid:
                case NpcTemplateMgr.eBodyType.Reptile:
                case NpcTemplateMgr.eBodyType.Undead:
                case NpcTemplateMgr.eBodyType.Elemental:
                default:
                    return 20; // Standard 20%
            }
        }

        /// <summary>
        /// Calculates the probability for a specific tier to drop based on mob level.
        /// Scales similarly to CalculateMetalBarChance.
        /// </summary>
        private int CalculateTierChance(int mobLevel, int tier)
        {
            // Center points for tiers
            // Tier 1: Lvl 5, Tier 2: Lvl 15, Tier 3: Lvl 25, Tier 4: Lvl 35, Tier 5: Lvl 45
            int targetLevel = (tier * 10) - 5;
            int levelDifference = mobLevel - targetLevel;

            // Endgame scaling
            if (mobLevel >= 45)
            {
                if (tier == 5) return 65 + Math.Max(0, (mobLevel - 45)); // High chance for Endgame tier
                if (tier == 4) return 15;
                if (tier == 3) return 5;
                return 0;
            }

            if (levelDifference >= -5 && levelDifference <= 5)
            {
                return 65; // Prime level range for this tier
            }
            else if (levelDifference > 5 && levelDifference <= 12)
            {
                return 20; // Mob is higher level, small chance to drop this lower tier
            }
            else if (levelDifference >= -12 && levelDifference < -5)
            {
                return 10; // Mob is slightly lower level, rare chance to drop higher tier
            }
            else if (levelDifference > 12 && levelDifference <= 20)
            {
                return 5;  // Mob is much higher level, very rare to drop this lower tier
            }

            return 0; // Out of range
        }

        public override LootList GenerateLoot(GameObject mobObj, GameObject killerObj)
        {
            LootList loot = base.GenerateLoot(mobObj, killerObj);

            GameNPC mob = mobObj as GameNPC;
            GamePlayer player = (killerObj as GameLiving)?.GetController() as GamePlayer;

            if (mob == null || player == null)
                return loot;

            // 1. Check Region Restrictions
            eRegionCategory regionCat = RegionMapper.GetCategoryFromRegionID(mob.CurrentRegionID);
            if (regionCat == eRegionCategory.Restricted)
                return loot;

            // 2. Base chance & Player Loot Modifier
            int baseChance = GetBaseDropChance((NpcTemplateMgr.eBodyType)mob.BodyType);
            int finalChance = Math.Min(100, Math.Max(0, baseChance + player.LootChance));

            // Roll to see if we drop any mineral at all
            if (!Util.Chance(finalChance))
                return loot;

            eRegionEnvironment env = RegionMapper.GetEnvironmentFromRegionID(mob.CurrentRegionID);
            NpcTemplateMgr.eBodyType bodyType = (NpcTemplateMgr.eBodyType)mob.BodyType;

            // Fast Category lookup
            eCreatureCategory creatureCategory = eCreatureCategory.None;
            if (_modelToCreatureCategory.TryGetValue(mob.Model, out eCreatureCategory parsedCat))
            {
                creatureCategory = parsedCat;
            }

            // 3. Roll for Tiers & select a mineral
            var droppedMinerals = new LootList(1);

            // Iterate through Tiers 1 to 5 to see which one succeeds
            for (int tier = 1; tier <= 5; tier++)
            {
                int tierChance = CalculateTierChance(mob.Level, tier);
                if (tierChance > 0 && Util.Chance(tierChance))
                {
                    ItemTemplate chosenMineral = GetBestMineralMatch(tier, env, bodyType, creatureCategory);

                    if (chosenMineral != null)
                    {
                        droppedMinerals.AddFixed(chosenMineral, 1);
                        break; // Only drop 1 tier type per successful roll to prevent loot flooding
                    }
                }
            }

            foreach (var mineral in droppedMinerals.GetLoot())
            {
                loot.AddFixed(mineral, 1);
            }

            return loot;
        }

        /// <summary>
        /// Highly intelligent Scoring System to find the best matching mineral for the Tier, Environment, BodyType, and CreatureType.
        /// </summary>
        private ItemTemplate GetBestMineralMatch(int tier, eRegionEnvironment env, NpcTemplateMgr.eBodyType bodyType, eCreatureCategory creatureCat)
        {
            var tierMinerals = _mineralDefinitions.Where(m => m.Tier == tier).ToList();
            if (tierMinerals.Count == 0) return null;

            // Score-based matching (Highest score wins)
            // +3 points for Environment Match (Theme is very important)
            // +2 points for Specific CreatureType Match
            // +1 point for General BodyType Match
            var scoredMinerals = tierMinerals.Select(m => new
            {
                Mineral = m,
                Score = (m.FavoredEnvironments.Contains(env) ? 3 : (m.FavoredEnvironments.Contains(eRegionEnvironment.Global) ? 1 : 0)) +
                        (m.FavoredCreatureCategories.Contains(creatureCat) ? 2 : 0) +
                        (m.FavoredBodyTypes.Contains(bodyType) ? 1 : 0)
            }).OrderByDescending(x => x.Score).ToList();

            // Find all minerals that tied for the highest score
            int highestScore = scoredMinerals.First().Score;
            var bestMatches = scoredMinerals.Where(x => x.Score == highestScore).Select(x => x.Mineral).ToList();

            // Pick a random mineral from the tied winners
            MineralDef chosenDef = bestMatches[Util.Random(0, bestMatches.Count - 1)];

            // Return the actual ItemTemplate if it's cached
            if (_mineralTemplates.TryGetValue(chosenDef.Id_nb, out ItemTemplate template))
            {
                return template;
            }

            return null;
        }

        /// <summary>
        /// Loads templates from Database and caches them.
        /// </summary>
        private void LoadTemplatesFromDatabase()
        {
            var mineralIds = _mineralDefinitions.Select(m => m.Id_nb).ToList();

            var templates = GameServer.Database.SelectObjects<ItemTemplate>(it => mineralIds.Contains(it.Id_nb));

            foreach (var t in templates)
            {
                if (!_mineralTemplates.ContainsKey(t.Id_nb))
                {
                    _mineralTemplates.Add(t.Id_nb, t);
                }
            }
            log.Info($"[LootGeneratorMinerals] Cached {_mineralTemplates.Count} mineral templates.");
        }

        /// <summary>
        /// Categorizes the raw ID list into Themes (Environments), BodyTypes, and specific CreatureTypes.
        /// </summary>
        private static void InitializeMineralDefinitions()
        {
            void AddMineral(int tier, string id, eRegionEnvironment[] envs, NpcTemplateMgr.eBodyType[] bodies, eCreatureCategory[] creatureCats)
            {
                _mineralDefinitions.Add(new MineralDef
                {
                    Id_nb = id,
                    Tier = tier,
                    FavoredEnvironments = envs.ToList(),
                    FavoredBodyTypes = bodies.ToList(),
                    FavoredCreatureCategories = creatureCats.ToList()
                });
            }

            // --- CATEGORY POOLS (WITH OVERWORLD/GLOBAL COMBINATIONS) ---
            var EarthEnvs = new[] { eRegionEnvironment.VolcanicAndEarth, eRegionEnvironment.CavesAndNature };
            var EarthAndGlobal = new[] { eRegionEnvironment.VolcanicAndEarth, eRegionEnvironment.CavesAndNature, eRegionEnvironment.Global };

            var MagicEnvs = new[] { eRegionEnvironment.MagicalAndEthereal };
            var MagicAndGlobal = new[] { eRegionEnvironment.MagicalAndEthereal, eRegionEnvironment.Global };

            var UndeadEnvs = new[] { eRegionEnvironment.CryptsAndUndead, eRegionEnvironment.DemonicAndAbyss };
            var UndeadAndGlobal = new[] { eRegionEnvironment.CryptsAndUndead, eRegionEnvironment.DemonicAndAbyss, eRegionEnvironment.Global };

            var DemonEnvs = new[] { eRegionEnvironment.DemonicAndAbyss, eRegionEnvironment.VolcanicAndEarth };
            var DemonAndGlobal = new[] { eRegionEnvironment.DemonicAndAbyss, eRegionEnvironment.VolcanicAndEarth, eRegionEnvironment.Global };

            var WaterEnvs = new[] { eRegionEnvironment.GlacialAndAquatic };
            var WaterAndGlobal = new[] { eRegionEnvironment.GlacialAndAquatic, eRegionEnvironment.Global };

            var BeastEnvs = new[] { eRegionEnvironment.BeastAndGoblinoid, eRegionEnvironment.CavesAndNature };
            var BeastAndGlobal = new[] { eRegionEnvironment.BeastAndGoblinoid, eRegionEnvironment.CavesAndNature, eRegionEnvironment.Global };

            // --- BODY AND CREATURE TYPES ---
            var BasicBodies = new[] { NpcTemplateMgr.eBodyType.Humanoid, NpcTemplateMgr.eBodyType.Giant, NpcTemplateMgr.eBodyType.Reptile };
            var MagicBodies = new[] { NpcTemplateMgr.eBodyType.Elemental, NpcTemplateMgr.eBodyType.Demon, NpcTemplateMgr.eBodyType.Magical };
            var UndeadBodies = new[] { NpcTemplateMgr.eBodyType.Undead };
            var BeastBodies = new[] { NpcTemplateMgr.eBodyType.Animal, NpcTemplateMgr.eBodyType.Insect, NpcTemplateMgr.eBodyType.Dragon };

            var BasicCreatures = new[] { eCreatureCategory.Basic };
            var BeastCreatures = new[] { eCreatureCategory.Beast };
            var ReptileCreatures = new[] { eCreatureCategory.Reptile };
            var BugCreatures = new[] { eCreatureCategory.Bug };
            var UndeadCreatures = new[] { eCreatureCategory.Undead };
            var MagicCreatures = new[] { eCreatureCategory.Magic };
            var WaterCreatures = new[] { eCreatureCategory.Water };

            // ================= TIER 1 =================
            AddMineral(1, "malachite_nodule", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "cassiterite_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "hematite_chunk", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "bog_iron_concretion", BeastAndGlobal, BeastBodies, BeastCreatures);
            AddMineral(1, "azurite_crystal", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(1, "chalcocite_stone", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "bornite_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "cuprite_chunk", UndeadEnvs, UndeadBodies, UndeadCreatures);
            AddMineral(1, "stannite_rock", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(1, "goethite_ore", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "limonite_crust", BeastAndGlobal, BeastBodies, BeastCreatures);
            AddMineral(1, "siderite_nodule", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(1, "taconite_shard", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "meteoric_iron_dust", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(1, "chalcopyrite_fragment", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(1, "tetrahedrite_pebble", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(1, "enargite_rock", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(1, "tenantite_stone", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "chrysocolla_chunk", EarthAndGlobal, BasicBodies, ReptileCreatures);
            AddMineral(1, "covellite_flake", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(1, "arsenopyrite_stone", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(1, "pyrrhotite_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "marcasite_nodule", UndeadAndGlobal, UndeadBodies, UndeadCreatures);
            AddMineral(1, "galena_shard", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "cerussite_stone", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(1, "anglesite_crystal", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(1, "bauxite_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "sphalerite_grit", EarthEnvs, BeastBodies, BugCreatures);
            AddMineral(1, "smithsonite_chunk", UndeadEnvs, UndeadBodies, UndeadCreatures);
            AddMineral(1, "hemimorphite_rock", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "willemite_crystal", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(1, "zincite_nodule", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(1, "hydrozincite_crust", WaterAndGlobal, BeastBodies, WaterCreatures);
            AddMineral(1, "franklinite_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);

            // ================= TIER 2 =================
            AddMineral(2, "bituminous_coal_lump", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(2, "quartz_geode", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "dolomite_nodule", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "pyrite_cluster", BeastAndGlobal, BeastBodies, BeastCreatures);
            AddMineral(2, "anthracite_coal_chunk", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(2, "lignite_coal_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "graphite_flake", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(2, "clear_quartz_shard", WaterAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(2, "smoky_quartz_crystal", UndeadAndGlobal, UndeadBodies, UndeadCreatures);
            AddMineral(2, "rose_quartz_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "amethyst_geode_fragment", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(2, "citrine_crystal_shard", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(2, "agate_nodule", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "onyx_pebble", UndeadAndGlobal, UndeadBodies, UndeadCreatures);
            AddMineral(2, "carnelian_stone", EarthAndGlobal, BasicBodies, ReptileCreatures);
            AddMineral(2, "chalcedony_chunk", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(2, "jasper_rock", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "flint_nodule", BeastAndGlobal, BeastBodies, BugCreatures);
            AddMineral(2, "chert_shard", BeastEnvs, BeastBodies, BeastCreatures);
            AddMineral(2, "magnesite_stone", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(2, "brucite_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "talc_rock", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "chlorite_flake", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(2, "epidote_crystal", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(2, "prehnite_stone", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "wollastonite_chunk", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(2, "rhodonite_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "rhodochrosite_crystal", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(2, "manganite_stone", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(2, "pyrolusite_nodule", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(2, "bixbyite_shard", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "hausmannite_rock", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(2, "braunite_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(2, "psilomelane_crust", UndeadEnvs, UndeadBodies, UndeadCreatures);
            AddMineral(2, "barite_rose", UndeadAndGlobal, UndeadBodies, UndeadCreatures);

            // ================= TIER 3 =================
            AddMineral(3, "pentlandite_orepiece", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(3, "spinel_gemstone", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(3, "silvery_mithril_vein", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(3, "topaz_rough", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(3, "zircon_crystal", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(3, "smaltite_nodule", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(3, "cobaltite_rock", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(3, "skutterudite_stone", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(3, "erythrite_crystal", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(3, "glaucodot_pebble", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(3, "garnierite_chunk", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(3, "millerite_stone", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(3, "niccolite_nodule", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(3, "chloanthite_rock", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(3, "breithauptite_pebble", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(3, "beryl_crystal", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(3, "aquamarine_rough", WaterAndGlobal, BeastBodies, WaterCreatures);
            AddMineral(3, "emerald_flaw", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(3, "heliodor_stone", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(3, "morganite_rough", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(3, "tourmaline_crystal", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(3, "schorl_rock", UndeadAndGlobal, UndeadBodies, UndeadCreatures);
            AddMineral(3, "dravite_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(3, "elbaite_stone", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(3, "garnet_rough", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(3, "almandine_crystal", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(3, "pyrope_stone", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(3, "spessartine_nodule", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(3, "grossular_pebble", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(3, "andradite_rock", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(3, "uvarovite_crystal", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(3, "spodumene_chunk", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(3, "kunzite_rough", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(3, "hiddenite_stone", UndeadEnvs, UndeadBodies, UndeadCreatures);
            AddMineral(3, "petalite_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(3, "lepidolite_flake", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(3, "amblygonite_rock", UndeadAndGlobal, UndeadBodies, UndeadCreatures);
            AddMineral(3, "turquoise_rough", EarthAndGlobal, BasicBodies, ReptileCreatures);
            AddMineral(3, "variscite_nodule", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(3, "wavellite_crystal", WaterAndGlobal, BeastBodies, WaterCreatures);

            // ================= TIER 4 =================
            AddMineral(4, "corundum_fragment", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(4, "sapphire_rough", WaterAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(4, "stibnite_asterite", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(4, "diamond_rough", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(4, "alexandrite_shard", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(4, "ruby_rough", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(4, "padparadscha_stone", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(4, "rutile_crystal", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(4, "anatase_pebble", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(4, "brookite_rock", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(4, "ilmenite_chunk", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(4, "perovskite_stone", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(4, "titanite_crystal", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(4, "columbite_nodule", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(4, "tantalite_rock", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(4, "wolframite_chunk", BeastAndGlobal, BeastBodies, BeastCreatures);
            AddMineral(4, "scheelite_crystal", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(4, "ferberite_pebble", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(4, "huebnerite_stone", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(4, "molybdenite_flake", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(4, "wulfenite_crystal", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(4, "powellite_rock", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(4, "bismutite_nodule", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(4, "bismuthinite_stone", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(4, "cinnabar_chunk", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(4, "realgar_crystal", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(4, "orpiment_rock", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(4, "stibnite_pebble", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(4, "kermesite_stone", UndeadAndGlobal, UndeadBodies, UndeadCreatures);
            AddMineral(4, "tetrahedrite_crystal", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(4, "bournonite_nodule", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(4, "pyrargyrite_rock", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(4, "proustite_crystal", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(4, "stephanite_pebble", EarthEnvs, BasicBodies, BasicCreatures);
            AddMineral(4, "polybasite_stone", EarthAndGlobal, BasicBodies, BasicCreatures);
            AddMineral(4, "acanthite_chunk", UndeadEnvs, UndeadBodies, UndeadCreatures);
            AddMineral(4, "argentite_crystal", UndeadAndGlobal, UndeadBodies, UndeadCreatures);
            AddMineral(4, "chlorargyrite_rock", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(4, "bromargyrite_pebble", UndeadAndGlobal, UndeadBodies, UndeadCreatures);
            AddMineral(4, "iodargyrite_stone", MagicAndGlobal, MagicBodies, MagicCreatures);

            // ================= TIER 5 (Endgame / Mythical) =================
            AddMineral(5, "ilmenite_nethershard", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(5, "black_opal_void", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "labradorite_astral", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "benitoite_astral", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(5, "painite_relic", UndeadAndGlobal, UndeadBodies, UndeadCreatures);
            AddMineral(5, "taaffeite_relic", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "chaos_emerald_flaw", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(5, "law_stone_fragment", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "runeblade_splinter", UndeadEnvs, UndeadBodies, UndeadCreatures);
            AddMineral(5, "ariyoch_blood_ruby", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "xiombarg_tear_crystal", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(5, "mabden_soul_gem", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "melnibonean_jade", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "chronocrystal_shard", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "entropy_geode", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(5, "void_touched_obsidian", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "abyssal_pearl", WaterAndGlobal, MagicBodies, WaterCreatures);
            AddMineral(5, "star_metal_slag", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "astral_diamond_dust", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "nether_quartz_anomaly", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(5, "eldritch_amethyst", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "warped_space_spinel", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(5, "demonic_bloodstone", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "celestial_celestite", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "ethereal_moonstone", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(5, "phantom_quartz_core", UndeadAndGlobal, UndeadBodies, UndeadCreatures);
            AddMineral(5, "twilight_topaz", UndeadEnvs, UndeadBodies, UndeadCreatures);
            AddMineral(5, "eclipse_sunstone", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "paradox_prism", MagicEnvs, MagicBodies, MagicCreatures);
            AddMineral(5, "singularity_stone", DemonAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "aetherial_amber", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "pandemonium_pyrite", DemonEnvs, MagicBodies, MagicCreatures);
            AddMineral(5, "leylined_lapis", MagicAndGlobal, MagicBodies, MagicCreatures);
            AddMineral(5, "oblivion_onyx", UndeadAndGlobal, UndeadBodies, UndeadCreatures);
            AddMineral(5, "hyperdimensional_halite", MagicEnvs, MagicBodies, MagicCreatures);
        }
    }
}