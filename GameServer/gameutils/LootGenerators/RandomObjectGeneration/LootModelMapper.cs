using System;
using System.Collections.Generic;
using DOL.GS;
using DOL.Events;

namespace DOL.GS
{
    public static class LootModelMapper
    {
        public struct ModelDefinition
        {
            public string BaseName;
            public eObjectType ObjType;
            public eInventorySlot Slot;
            public eRealm RealmOrigin;
        }

        private static Dictionary<int, ModelDefinition> _definitions = new Dictionary<int, ModelDefinition>();

        [ScriptLoadedEvent]
        public static void OnScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            LoadDefinitions();
            Console.WriteLine($"[SmartLoot] Loaded {_definitions.Count} model definitions into the Loot Mapper.");
        }

        public static void LoadDefinitions()
        {
            _definitions.Clear();

            // ==========================================================
            // WEAPONS
            // ==========================================================

            // 1-Handed Swords
            RegisterSwords1H(eInventorySlot.RightHandWeapon, new[] {
                "3, Briton Short Sword", "4, Briton Longsword", "5, Briton Broadsword", "8, Scimitar", "10, Bastard Sword",
                "445, Celtic Short Sword", "444, Celtic Falcata", "473, Celtic Bastard Sword", "447, Celtic Broadsword", "446, Celtic Longsword",
                "117, Celtick Hooked Sword", "118, Celtic Sickle", "129, Norse Short Sword", "130, Norse Broad Sword", "131, Norse Long Sword",
                "313, Norse Bastard Sword", "133, Norse Two Sword", "212, Celtic Great Falcata", "225, Briton Sabre", "227, Norse Cleaver",
                "655, Norse Dwarven Short Sword", "673, Briton Longsword Hilt", "670, Norse Longsword Hilt", "674, Celtic Longsword Hilt",
                "421, Troll 1H Broad Sword", "425, Troll Short Sword",
                "877, Alb Cinquedea", "879, Alb Falchion", "896, Hib Elven Long Sword", "897, Hib Elven Short Sword",
                "899, Hib Elven Firbolg L Sword", "900, Hib Elven Firbolg S Sword", "901, Hib Leaf Point Broadsword", "903, Hib Leaf Short Sword",
                "948, Troll Broad Sword", "952, Troll Short Sword", "1015, Kobold Long Sword", "1017, Kobold Short Sword",
                "1670, Battlersword 01", "1820, Bonedancer 1 handed sword", "1920, Lab 1h sword thrust offhand", "1921, Lab 1h sword thrust mainhand",
                "1941, Alb Cinquedea Air", "1942, Alb Cinquedea Earth", "1943, Alb Cinquedea Fire", "1944, Alb Cinquedea Water",
                "1945, Alb Falchion Air", "1946, Alb Falchion Earth", "1947, Alb Falchion Fire", "1948, Alb Falchion Water",
                "1969, Hib Elven Firbolg L Sword Air", "1970, Hib Elven Firbolg L Sword Earth", "1971, Hib Elven Firbolg L Sword Fire",
                "1972, Hib Elven Firbolg L Sword Water", "1973, Hib Elven Firbolg S Sword Air", "1974, Hib Elven Firbolg S Sword Earth",
                "1975, Hib Elven Firbolg S Sword Fire", "1976, Hib Elven Firbolg S Sword Water", "2033, Troll 1H Broad Sword Air",
                "2034, Troll 1H Broad Sword Earth", "2035, Troll 1H Broad Sword Fire", "2036, Troll 1H Broad Sword Water",
                "2112, Battlersword 1H", "2195, Toa_S_1H_Kopesh01", "2203, Toa_A_1H_Aerussword01", "2209, Toa_A_1H_Wakazashi01",
                "4491, Alb Cinquedea Body", "4492, Alb Cinquedea Energy", "4493, Alb Falchion Body", "4494, Alb Falchion Energy",
                "4505, Hib Elven Firbolg L Sword Body", "4506, Hib Elven Firbolg L Sword Energy", "4507, Hib Elven Firbolg S Sword Body",
                "4508, Hib Elven Firbolg S Sword Energy", "4537, Troll 1H Broad Sword Body", "4538, Troll 1H Broad Sword Energy",
                "4904, 1H Saw", "4906, Sickle", "4943, Twilight Scimitar", "4944, Caladbolg", "4945, Buster Sword", "4947, Sword Ivar",
                "4948, Eldred Scimitar", "4954, Master Sword", "4955, Wooden Sword", "4967, Daemon Machete"
            });

            // 2-Handed Swords
            RegisterSwords2H(eInventorySlot.TwoHandWeapon, new[] {
                "6, Briton 2h Sword", "7, Great Sword", "100, Celtic Two Handed Sword", "107, Celtic Great Sword",
                "218, Briton 2h Scimitar", "231, Norse Dwarven 2h Sword", "233, Norse 2h War Cleaver", "236, Briton 2h Sword Hilt",
                "239, Norse 2h Sword Hilt", "241, Celtic Great Sword Hilt", "433, Kobold Great Sword", "436, Troll Great Sword",
                "572, Norse Great Sword", "907, Hib 2H Elf Great Sword", "910, Hib 2H Firbolg Troll Splitter", "911, Hib 2H Leaf Point Sword",
                "957, Troll Great Sword", "1819, Bonedancer 2 handed sword", "1899, Lab 2 Handed Sword Thrust", "1900, Lab 2 Handed Sword Slash",
                "1901, Alb 2H Slash Air", "1902, Alb 2H Slash Earth", "1903, Alb 2H Slash Fire", "1904, Alb 2H Slash Water",
                "1981, Hib 2H Firbolg Troll Splitter Air", "1982, Hib 2H Firbolg Troll Splitter Earth", "1983, Hib 2H Firbolg Troll Splitter Fire",
                "1984, Hib 2H Firbolg Troll Splitter Water", "2043, Hib DragonSlayer 2H Sword", "2044, Hib DragonSlayer 2H Sword Slash",
                "2057, Kobold Great Sword Air", "2058, Kobold Great Sword Earth", "2059, Kobold Great Sword Fire", "2060, Kobold Great Sword Water",
                "2082, Mid DragonSlayer 2H Sword", "2083, Mid DragonSlayer 2H Sword Slash", "2117, Alb DragonSlayer 2H Sword",
                "2118, Alb DragonSlayer 2H Sword Slash", "2196, Toa_S_2H_Kopesh01", "2204, Toa_A_2H_Aerussword01", "2208, Toa_A_2H_Katana01",
                "2302, Pict 2 Handed Sword Thrust", "2303, Pict 2 Handed Sword Slash", "4471, Alb 2H Slash Body", "4472, Alb 2H Slash Energy",
                "4511, Hib 2H Firbolg Troll Splitter Body", "4512, Hib 2H Firbolg Troll Splitter Energy", "4549, Kobold Great Sword Body",
                "4550, Kobold Great Sword Energy", "4905, 2H Saw", "4946, Masamune"
            });

            // Daggers (Thrust/Piercing)
            RegisterDaggers(eInventorySlot.RightHandWeapon, new[] {
                "1, Briton Dagger", "21, Dirk", "22, Rapier", "24, Epee", "25, Main Gauche", "29, Foil", "30, Gladius",
                "71, Stilletto", "94, Celtic Dagger", "95, Celtic Stilletto", "111, Celtic Dirk", "112, Celtic Rapier",
                "113, Curved Dagger", "216, Celtic Guarded Rapier", "224, Briton Jambiya", "226, Briton Guarded Rapier",
                "422, Troll Dagger", "571, Norse Dagger", "876, Alb Breton Toothpick", "885, Alb Duelists Dagger", "886, Alb Duelists Rapier",
                "887, Alb Parrying Dagger", "888, Alb Parrying Rapier", "889, Alb Highlander Dirk", "895, Hib Elven Dagger",
                "898, Hib Elven Firbolg Dagger", "902, Hib Leaf Point Dagger", "943, Hib Lurikeen Dagger", "944, Hib Lurkeen Duel Dagger",
                "945, Hib Lurkeen Duel Rapier", "946, Hib Lurikeen Rapier", "949, Troll Dagger", "1013, Kobold Dagger",
                "1668, Traitorsdagger 01", "1953, Alb Duelists Dagger Air", "1954, Alb Duelists Dagger Earth", "1955, Alb Duelists Dagger Fire",
                "1956, Alb Duelists Dagger Water", "1957, Alb Duelists Rapier Air", "1958, Alb Duelists Rapier Earth", "1959, Alb Duelists Rapier Fire",
                "1960, Alb Duelists Rapier Water", "4497, Alb Duelists Dagger Body", "4498, Alb Duelists Dagger Energy", "4499, Alb Duelists Rapier Body",
                "4500, Alb Duelists Rapier Energy", "4964, Ceremonial Dagger"
            });

            // Axes 1H
            RegisterAxes1H(eInventorySlot.RightHandWeapon, new[] {
                "2, Briton Hand Axe", "9, Battle Axe", "16, War Mattock", "26, Briton Bill", "73, War Axe",
                "185, War Axe (2)",
                "222, Briton Poleaxe", "315, Norse Spiked Axe", "316, Norse Bearded Axe", "318, Norse Large Axe", "319, Norse War Axe",
                "333, Norse Throwing Axe",
                "573, Norse 1h Double Axe", "578, Norse Hand Axe", "873, Alb Sabre Axe",
                "878, Alb Coffin Axe", "880, Alb War Axe", "940, Hib Adze", "941, Hib Barbed Adze", "942, Hib Firbolg Adze", "947, Hib War Adze",
                "951, Troll Hand Axe", "953, Troll War Axe", "1010, Dwarven 1H Hand Axe", "1011, Dwarven 1H War Axe", "1014, Kobold Hand Axe",
                "1018, Kobold War Axe", "1023, Troll Hand Axe", "1025, Troll 1H War Axe", "1810, Croc Tooth Axe", "1811, Traitors Axe",
                "1826, Bonedancer 1 handed axe", "1922, Lab 1h axe offhand", "1923, Lab 1h axe mainhand", "1959, Scorched Lab 1h axe offhand",
                "1963, Scorched Lab 1h axe mainhand", "2008, Dragonsworn 1h axe mainhand", "2009, Dragonsworn 1h axe offhand",
                "2009, Hib Firbolg Adze Air", "2010, Hib Firbolg Adze Earth", "2011, Hib Firbolg Adze Fire", "2012, Hib Firbolg Adze Water",
                "2013, Hib War Adze Air", "2014, Hib War Adze Earth", "2015, Hib War Adze Fire", "2016, Hib War Adze Water",
                "2029, Dwarven 1H War Axe Air", "2030, Dwarven 1H War Axe Earth", "2031, Dwarven 1H War Fire", "2032, Dwarven 1H War Water",
                "2037, Troll 1H War Axe Air", "2038, Troll 1H War Axe Earth", "2039, Troll 1H War Axe Fire", "2040, Troll 1H War Axe Water",
                "2053, Hib DragonSlayer 1h axe", "2054, Hib DragonSlayer 1h axe", "2092, Mid DragonSlayer 1h axe", "2093, Mid DragonSlayer 1h axe",
                "2109, Toa Malice Ax 1H", "2127, Alb DragonSlayer 1h axe", "2128, Alb DragonSlayer 1h axe", "2216, Toa_V_1H_Magmaaxe01",
                "2312, Pict 1h axe", "2313, Pict 1h axe", "2985, Battle Axe", "4525, Hib Firbolg Adze Body", "4526, Hib Firbolg Adze Energy",
                "4527, Hib War Adze Body", "4528, Hib War Adze Energy", "4535, Dwarven 1H War Body", "4536, Dwarven 1H War Energy",
                "4539, Troll 1H War Axe Body", "4540, Troll 1H War Axe Energy", "4967, Daemon Machete"
            });

            // Axes 2H
            RegisterAxes2H(eInventorySlot.TwoHandWeapon, new[] {
                "72, Great Axe", "190, Lochaber Axe", "221, Briton Bardiche", "317, Norse Great Axe",
                "577, Norse 2h Battleaxe", "955, Troll Great Axe",
                "1027, Dwarven 2H Great Axe", "1030, Kobold Great Axe", "1033, Troll Great Axe", "1825, Bonedancer 2 handed axe",
                "1904, Lab 2 Handed Axe", "1945, Scorched Lab 2 Handed Axe", "2016, Dragonsworn 2 Handed Axe", "2046, Hib DragonSlayer 2H Axe",
                "2049, Kobold Great Axe Air", "2050, Kobold Great Earth", "2051, Kobold Great Axe Fire", "2052, Kobold Great Water",
                "2085, Mid DragonSlayer 2 Handed Axe", "2110, Toa Malice Ax 2H", "2120, Alb DragonSlayer 2 Handed Axe", "2217, Toa_V_2H_Magmaaxe01",
                "2305, Pict 2 Handed Axe", "4545, Kobold Great Axe Body", "4546, Kobold Great Axe Energy", "4965, Orc Battleaxe", "4966, Arbiter Axe"
            });

            // Hammers 1H
            RegisterHammers1H(eInventorySlot.RightHandWeapon, new[] {
                "11, Club", "12, Hammer", "13, Mace", "14, Flanged Mace", "15, War Hammer", "18, Morning Star", "20, Spiked Mace",
                "145, Norse Greathammer 1H",
                "213, Celtic Sledgehammer", "214, Celtic Pickhammer", "220, Briton 1H Special Mace", "223, Briton Spikedhammer",
                "229, Norse Spiked Hammer", "320, Norse Small Hammer 1H", "321, Norse Hammer 1H", "322, Norse Warhammer 1H",
                "323, Norse Pickhammer 1H", "324, Norse Greathammer 1H", "334, Norse Throwing Hammer",
                "449, Celtic Club", "450, Celtic Mace", "451, Celtic Spiked Mace", "452, Spiked Club", "461, Celtic Hammer",
                "613, Spiked Club Item", "640, Celtic Sledgehammer", "641, Celtic Pickhammer", "647, Briton 1H Special Mace",
                "650, Briton Spikedhammer", "656, Norse Spiked Hammer", "853, Alb Avalonian Mace", "854, Alb BishopsMace",
                "855, Alb Coffin Hammer Mace", "856, Alb Coffin Mace Mace", "870, Alb Bishops Reach", "871, Alb Footmans Pick",
                "913, Hib Celtic Hammer", "914, Hib Celtic Mace", "950, Troll Hammer", "954, Troll War Hammer", "1009, Dwarven 1H Hammer",
                "1012, Dwarven 1H War Hammer", "1016, Kobold Sap", "1019, Kobold War Club", "1022, Troll 1H Hammer", "1026, Troll 1H War Hammer",
                "1806, Malice Hammer 1 Handed", "1812, Battler Hammer 1 handed", "1823, Bonedancer 1 handed hammer", "1913, Alb BishopsMace Air",
                "1914, Alb BishopsMace Earth", "1915, Alb BishopsMace Fire", "1916, Alb BishopsMace Water", "1917, Alb Coffin Mace Mace Air",
                "1918, Alb Coffin Mace Mace Earth", "1919, Alb Coffin Mace Fire", "1920, Alb Coffin Mace Water", "1918, Lab 1h hammer offhand",
                "1919, Lab 1h hammer mainhand", "1958, Scorched Lab 1h hammer offhand", "1962, Scorched Lab 1h hammer mainhand",
                "1985, Hib Celtic Hammer Air", "1986, Hib Celtic Hammer Earth", "1987, Hib Celtic Hammer Fire", "1988, Hib Celtic Hammer Water",
                "1989, Hib Firbolg Hammer Air", "1990, Hib Firbolg Hammer Earth", "1991, Hib Firbolg Hammer Fire", "1992, Hib Firbolg Hammer Water",
                "2010, Dragonsworn 1h hammer", "2011, Dragonsworn 1h hammer", "2041, Troll 1H War Hammer Air", "2042, Troll 1H War Hammer Earth",
                "2043, Troll 1H War Hammer Fire", "2044, Troll 1H War Hammer Water", "2051, Hib DragonSlayer 1h hammer",
                "2052, Hib DragonSlayer 1h hammer", "2090, Mid DragonSlayer 1h hammer", "2091, Mid DragonSlayer 1h hammer",
                "2125, Alb DragonSlayer 1h hammer", "2126, Alb DragonSlayer 1h hammer", "2198, Toa_S_Cr_Symbolscepter01",
                "2205, Toa_A_Cr_Aerusmace01", "2214, Toa_V_1H_Magmahammer01", "2310, Pict 1h hammer", "2311, Pict 1h hammer",
                "4477, Alb BishopsMace body", "4478, Alb BishopsMace Energy", "4479, Alb Coffin Mace body", "4480, Alb Coffin Mace Energy",
                "4513, Hib Celtic Hammer Body", "4514, Hib Celtic Hammer Energy", "4515, Hib Firbolg Hammer Body", "4516, Hib Firbolg Hammer Energy",
                "4541, Troll 1H War Hammer Body", "4542, Troll 1H War Hammer Energy", "4961, Volendrung 1h", "4963, Mace of Molag"
            });

            // Hammers 2H
            RegisterHammers2H(eInventorySlot.TwoHandWeapon, new[] {
                "17, Great Hammer", "462, Celtic Great Hammer",
                "192, Lucerne Hammer", "217, Briton 2H Arch Mace", "219, Briton 2H War Pick", "232, Norse 2H Spiked Hammer",
                "463, 2H Spiked Mace", "474, 2H Shilelaugh", "574, Norse 2H Hammer", "575, Norse 2H Warhammer", "576, Norse 2H Greathammer",
                "644, Briton 2H Arch Mace", "646, Briton 2H War Pick", "659, Norse 2H Spiked Hammer", "875, Alb Tall Hammer",
                "883, Alb Spiked Staff Mace", "884, Alb Staff Mace", "904, Hib 2H Hammer", "905, Hib 2H Mace", "906, Hib 2H Dire Club",
                "908, Hib 2H Firbolg Hammer", "909, Hib 2H Firbolg Mace", "912, Hib 2H Shod Shilelagh", "915, Hib Dire Mace",
                "916, Hib Firbolg Hammer", "917, Hib Firbolg 2H Mace", "956, Troll Great Hammer", "1028, Dwarven Great Hammer",
                "1031, Kobold 2H Great Club", "1034, Troll Great Hammer", "1671, Bruiserhammer 01", "1807, Battler Hammer 2 handed",
                "1808, Malice Hammer 2 handed", "1822, Bonedancer 2 handed hammer", "1893, Alb 2H Blunt Air", "1894, Alb 2H Blunt Earth",
                "1895, Alb 2H Blunt Fire", "1896, Alb 2H Blunt Water", "1903, Lab 2 Handed Hammer", "1937, Alb Tall Hammer Air",
                "1938, Alb Tall Hammer Earth", "1939, Alb Tall Hammer Fire", "1940, Alb Tall Hammer Water", "1944, Scorched Lab 2 Handed Hammer",
                "1949, Alb Spiked Staff Mace Air", "1950, Alb Spiked Staff Mace Earth", "1951, Alb Spiked Staff Mace Fire",
                "1952, Alb Spiked Staff Mace Water", "1977, Hib 2H Firbolg Hammer Air", "1978, Hib 2H Firbolg Hammer Earth",
                "1979, Hib 2H Firbolg Hammer Fire", "1980, Hib 2H Firbolg Hammer Water", "2017, Dragonsworn 2 Handed Hammer",
                "2045, Hib DragonSlayer 2 Handed Hammer", "2053, Kobold 2H Great Club Air", "2054, Kobold 2H Great Club Earth",
                "2055, Kobold 2H Great Club Fire", "2056, Kobold 2H Great Club Water", "2084, Mid DragonSlayer 2 Handed Hammer",
                "2113, Bruiserhammer 2H", "2119, Alb DragonSlayer 2 Handed Hammer", "2132, Scorpion Flex Mace", "2206, Toa_A_2H_Aerusmace01",
                "2215, Toa_V_2H_Magmahammer01", "2304, Pict 2 Handed Hammer", "4467, Alb 2H Blunt Body", "4468, Alb 2H Blunt Energy",
                "4489, Alb Tall Hammer Body", "4490, Alb Tall Hammer Energy", "4495, Alb Spiked Staff Mace Body", "4496, Alb Spiked Staff Mace Energy",
                "4509, Hib 2H Firbolg Hammer Body", "4510, Hib 2H Firbolg Hammer Energy", "4547, Kobold 2H Great Club Body",
                "4548, Kobold 2H Great Club Energy", "4962, Volendrung 2h"
            });

            // Polearms
            RegisterPolearms(eInventorySlot.TwoHandWeapon, new[] {
                "16, Trident", "23, Javelin", "27, Lance", "67, Halberd", "68, Lochaber Axe", "69, Pike",
                "98, Celtic Short Spear", "99, Celtic Spear", "114, Celtic Barbed Spear", "115, Celtic Long Spear", "116, Celtic War Spear",
                "181, Javelin", "189, Pike", "215, Celtic Hooked Spear", "230, Norse Battle Spear", "234, Briton Partizan",
                "328, Norse Simple Spear", "329, Norse Long Spear", "330, Norse Trident", "331, Norse Bill Spear", "332, Norse Big Spear",
                "458, Trident", "469, Celtic Spear", "470, Celtic Short Spear",
                "475, Celtic Barbed Spear", "476, Celtic Long Spear", "477, Celtic War Spear", "556, Special Hibernian Spear",
                "642, Celtic Hooked Spear", "657, Norse Battle Spear", "661, Briton Partizan", "872, Alb Military Fork",
                "874, Alb Saracen Glaive", "933, Hib Arrowhead Spear", "934, Hib Cruel Spear", "935, Hib Elven Spear",
                "936, Hib Firbolg Spear", "937, Hib Harpoon", "938, Hib Ribcatcher", "939, Hib Twisted Spear", "958, Troll Spear",
                "1004, 1 Handed Trident", "1029, Dwarven Spear", "1036, Troll Spear", "1603, Tridentofthegods 01", "1661, Spearofkings 01",
                "1662, Goldenspear 01", "1663, Tridentofthegods 01", "1897, Alb 2H Thrust Air", "1898, Alb 2H Thrust Earth",
                "1899, Alb 2H Thrust Fire", "1900, Alb 2H Thrust Water", "1901, Lab 2 Handed Spear Thrust", "1902, Lab 2 Handed Spear Slash",
                "1913, Lab Polearm Thrust", "1914, Lab Polearm Slash", "1915, Lab Polearm Blunt", "1929, Alb Military Fork Air",
                "1930, Alb Military Fork Earth", "1931, Alb Military Fork Fire", "1932, Alb Military Fork Water", "1933, Alb Saracen Glaive Air",
                "1934, Alb Saracen Glaive Earth", "1935, Alb Saracen Glaive Fire", "1936, Alb Saracen Glaive Water",
                "1951, Scorched Lab 2 Handed Spear Thrust", "1952, Scorched Lab Polearm Thrust", "1953, Scorched Lab 2 Handed Spear Slash",
                "1954, Scorched Lab Polearm Slash", "1955, Scorched Lab Polearm Blunt", "2000, Dragonsworn Polearm Blunt",
                "2001, Dragonsworn Polearm Slash", "2002, Dragonsworn Polearm Thrust", "2005, Hib Firbolg Spear Air",
                "2006, Hib Firbolg Spear Earth", "2007, Hib Firbolg Spear Fire", "2008, Hib Firbolg Spear Water", "2045, Dwarven Spear Air",
                "2046, Dwarven Spear Earth", "2047, Dwarven Spear Fire", "2048, Dwarven Spear Water", "2061, Hib DragonSlayer Polearm Thrust",
                "2062, Hib DragonSlayer Polearm Slash", "2063, Hib DragonSlayer Polearm Crush", "2100, Mid DragonSlayer Polearm Thrust",
                "2101, Mid DragonSlayer Polearm Slash", "2102, Mid DragonSlayer Polearm Crush", "2135, Alb DragonSlayer Polearm Thrust",
                "2136, Alb DragonSlayer Polearm Slash", "2137, Alb DragonSlayer Polearm Crush", "2190, Toa O Po 1Htrident01",
                "2191, Toa O Po 2Htrident01", "2320, Pict Polearm Thrust", "2321, Pict Polearm Slash", "2322, Pict Polearm Crush",
                "2467, Tridentofthegods 1H", "2468, Spearofkings 1H", "4469, Alb 2H Thrust Body", "4470, Alb 2H Thrust Energy",
                "4485, Alb Military Fork Body", "4486, Alb Military Fork Energy", "4487, Alb Saracen Glaive Body", "4488, Alb Saracen Glaive Energy",
                "4523, Hib Firbolg Spear Body", "4524, Hib Firbolg Spear Energy", "4543, Dwarven Spear Body", "4544, Dwarven Spear Energy"
            });

            // Scythes
            RegisterBatch(eObjectType.Scythe, eInventorySlot.TwoHandWeapon, new[] {
                "926, Hib Firbolg Great Scythe", "927, Hib Firbolg Scythe", "928, Hib Greatwar Scythe", "929, Hib Harvest Scythe",
                "930, Hib Martial Scythe", "931, Hib Scythe", "932, Hib War Scythe", "1809, Spear of the King Scythe", "1907, Lab Scythe",
                "1949, Scorched Lab Scythe", "2004, Dragonsworn Scythe", "2001, Hib Firbolg Scythe Air", "2002, Hib Firbolg Scythe Earth",
                "2003, Hib Firbolg Scythe Fire", "2004, Hib Firbolg Scythe Water", "2058, Hib DragonSlayer Scythe", "2097, Mid DragonSlayer Scythe",
                "2111, Serpent Scythe", "2132, Alb DragonSlayer Scythe", "2213, Toa_V_Sc_Magmascythe01", "2317, Pict Scythe",
                "4521, Hib Firbolg Scythe Body", "4522, Hib Firbolg Scythe Energy", "4949, Odin Scythe", "4960, Osiris Scythe"
            });

            // Hand to Hand
            RegisterBatch(eObjectType.HandToHand, eInventorySlot.RightHandWeapon, new[] {
                "959, BladedClawGreave", "960, BladedFangGreave", "961, BladedMoonClaw", "962, BladedMoonFang", "963, ClawGreave",
                "964, FangGreave", "965, GreatBladedClawGreave", "966, GreatBladedFangGreave", "967, GreatBladedMoonClaw",
                "968, GreatBladedMoonFang", "969, GreatClawGreave", "970, GreatFangGreave", "971, GreatMoonClaw", "972, GreatMoonFang",
                "973, LargeBladedClawGreave", "974, LargeBladedFangGreave", "975, LargeBladedMoonClaw", "976, LargeBladedMoonFang",
                "977, LargeClawGreave", "978, LargeFangGreave", "979, LargeMoonClaw", "980, LargeMoonFang", "981, MoonClaw",
                "982, MoonFang", "1924, Lab h2h slash offhand", "1925, Lab h2h slash mainhand", "1926, Lab h2h blunt offhand",
                "1927, Lab h2h blunt mainhand", "1928, Lab h2h thrust offhand", "1929, Lab h2h thrust mainhand", "1966, Scorched Lab h2h thrust offhand",
                "1967, Scorched Lab h2h thrust mainhand", "1968, Scorched Lab h2h slash offhand", "1969, Scorched Lab h2h slash mainhand",
                "1994, Dragonsworn h2h slash mainhand", "1995, Dragonsworn h2h slash offhand", "1996, Dragonsworn h2h thrust mainhand",
                "1997, Dragonsworn  h2h thrust offhand", "2021, GreatBladedClawGreave Air", "2022, GreatBladedClawGreave Earth",
                "2023, GreatBladedClawGreave Fire", "2024, GreatBladedClawGreave Water", "2025, GreatBladedFangGreave Air",
                "2026, GreatBladedFangGreave Earth", "2027, GreatBladedFangGreave Fire", "2028, GreatBladedFangGreave Water",
                "2066, Hib DragonSlayer h2h Thrust", "2067, Hib DragonSlayer h2h Thrust", "2068, Hib DragonSlayer h2h Slash",
                "2069, Hib DragonSlayer h2h Slash", "2070, Hib DragonSlayer h2h Crush", "2071, Hib DragonSlayer h2h Crush",
                "2105, Mid DragonSlayer h2h Thrust", "2106, Mid DragonSlayer h2h Thrust", "2107, Mid DragonSlayer h2h Slash",
                "2108, Mid DragonSlayer h2h Slash", "2109, Mid DragonSlayer h2h Crush", "2110, Mid DragonSlayer h2h Crush",
                "2140, Alb DragonSlayer h2h Thrust", "2141, Alb DragonSlayer h2h Thrust", "2142, Alb DragonSlayer h2h Slash",
                "2143, Alb DragonSlayer h2h Slash", "2144, Alb DragonSlayer h2h Crush", "2145, Alb DragonSlayer h2h Crush",
                "2197, Toa_S_Hth_Scorpiongreaves01", "2325, Pict h2h Thrust", "2326, Pict h2h Thrust", "2327, Pict h2h Slash",
                "2328, Pict h2h Slash", "2329, Pict h2h Crush", "2330, Pict h2h Crush", "4531, GreatBladedClawGreave Body",
                "4532, GreatBladedClawGreave Energy", "4533, GreatBladedFangGreave Body", "4534, GreatBladedFangGreave Energy"
            });

            // Fist Wraps
            RegisterBatch(eObjectType.FistWraps, eInventorySlot.RightHandWeapon, new[] {
                "1833, Fist Wrap", "1970, Scorched Lab h2h hand wrap", "1971, Scorched Lab h2h fist wrap", "1992, Dragonsworn fist wrap",
                "1993, Dragonsworn hand wrap", "3366, Alb Mauler Champion Glove", "3550, Hib Mauler Champion Glove", "3566, Alb Mauler Champion Glove",
                "3568, Mid Mauler Champion Glove", "3575, Serpent Fist Wrap", "3576, FistWrapGreave Air", "3577, FistWrapGreave Earth",
                "3578, FistWrapGreave Fire", "3579, FistWrapGreave Water", "4459, Hib Mauler Champion Glove DF",
                "4461, Alb Mauler Champion Glove DF", "4463, Mid Mauler Champion Glove DF", "4555, FistWrapGreave Body", "4556, FistWrapGreave Energy"
            });

            // Ranged
            RegisterRanged(eInventorySlot.DistanceWeapon, new[] {
                "471, Recurve Bow", "226, Crossbow", "132, Briton Longbow", "194, Briton Longbow",
                "471, Recurve Bow", "564, Norse Composite Bow",
                "569, Briton Shortbow", "570, Briton Greatbow", "848, Alb Brawlers Bow", "849, Alb Saracen Long Bow", "850, Alb Saracen Short Bow",
                "851, Alb Skinners Bow", "852, Alb Thorn Bow", "890, Alb Assault Crossbow", "891, Alb Brawlers Crossbow", "892, Alb Xbo Crossbow",
                "893, Alb Xbo Skinners Crossbow", "894, Alb Xbo Steel Crossbow", "918, Hib Brawlers Bow", "919, Hib Celtic Great Bow",
                "920, Hib Cutters Bow", "921, Hib Elven Long Bow", "922, Hib Elven Short Bow", "923, Hib Lurikeen Thorn Bow", "924, Hib Recurve Bow",
                "925, Hib Recurve Long Bow", "1037, Dwarven Crushing Bow", "1038, Kobold Fang Bow", "1039, Viking Beared Bow", "1666, Foolsbow 01",
                "1667, Braggartsbow 01", "1824, Bonedancer Bow", "1871, Bow (Item)", "1898, Lab Crossbow", "1905, Lab Short Bow", "1906, Lab Long Bow",
                "1905, Alb Saracen Short Bow Air", "1906, Alb Saracen Short Bow Earth", "1907, Alb Saracen Short Bow Fire",
                "1908, Alb Saracen Short Bow Water", "1909, Alb Thorn Bow Air", "1910, Alb Thorn Bow Earth", "1911, Alb Thorn Bow Fire",
                "1912, Alb Thorn Bow Water", "1939, Scorched Lab Crossbow", "1940, Scorched Lab Short Bow", "1941, Scorched Lab Long Bow",
                "1961, Alb Xbo Skinners Crossbow Air", "1962, Alb Xbo Skinners Crossbow Earth", "1963, Alb Xbo Skinners Crossbow Fire",
                "1964, Alb Xbo Skinners Crossbow Water", "1988, Dragonsworn Short Bow", "1993, Hib Elven Long Bow Air",
                "1994, Hib Elven Long Bow Earth", "1995, Hib Elven Long Bow Fire", "1996, Hib Elven Long Bow Water",
                "1997, Hib Elven Short Bow Air", "1998, Hib Elven Short Bow Earth", "1999, Hib Elven Short Bow Fire",
                "2000, Hib Elven Short Bow Water", "2020, Dragonsworn Crossbow", "2021, Dragonsworn Long Bow", "2040, Hib DragonSlayer Short Bow",
                "2041, Hib DragonSlayer Long Bow", "2042, Hib DragonSlayer Crossbow", "2061, Viking Beared Bow Air",
                "2062, Viking Beared Bow Earth", "2063, Viking Beared Bow Fire", "2064, Viking Beared Bow Water", "2079, Mid DragonSlayer Short Bow",
                "2080, Mid DragonSlayer Long Bow", "2081, Mid DragonSlayer Crossbow", "2114, Alb DragonSlayer Short Bow",
                "2115, Alb DragonSlayer Long Bow", "2116, Alb DragonSlayer Crossbow", "2207, Toa_A_2H_Aerusbow01", "2299, Pict Short Bow",
                "2300, Pict Long Bow", "2301, Pict Crossbow", "4473, Alb Saracen Short Bow Body", "4474, Alb Saracen Short Bow Energy",
                "4475, Alb Thorn Bow body", "4476, Alb Thorn Bow Energy", "4501, Alb Xbo Skinners Crossbow Body",
                "4502, Alb Xbo Skinners Crossbow Energy", "4517, Hib Elven Long Bow Body", "4518, Hib Elven Long Bow Energy",
                "4519, Hib Elven Short Bow Body", "4520, Hib Elven Short Bow Energy", "4551, Viking Beared Bow Body", "4552, Viking Beared Bow Energy"
            });

            // Staffs
            RegisterBatch(eObjectType.Staff, eInventorySlot.TwoHandWeapon, new[] {
                "19, Briton Mage Staff", "124, Celtic Staff",
                "158, Norse Rod 1", "159, Norse Rod 2", "160, Norse Rod 3", "161, Norse Wand 1", "162, Norse Wand 2", "163, Norse Wand 3",
                "197, Briton Mage Staff", "198, Quarterstaff", "199, Briton Shod Quarterstaff", "200, Briton Focus Staff", "246, Necro Staff",
                "327, Norse Staff", "441, Albion Mage Staff 01", "442, Albion Mage Staff 02", "443, Albion Mage Staff 03",
                "444, Albion Mage Staff 04", "445, Albion Mage Staff 05", "446, Necro Staff 01", "447, Necro Staff 02", "448, Hib Staff 01",
                "449, Hib Staff 02", "450, Hib Staff 03", "451, Hib Staff 04", "452, Hib Staff 05", "453, Hib Staff 06", "454, Hib Staff 07",
                "455, Hib Staff 08", "456, Hib Staff 09", "457, Hib Staff 10", "458, Hib Staff 11", "459, Hib Staff 12", "460, Hib Staff 13",
                "468, Celtic Staff", "552, Wand Item", "565, Norse Shod Staff", "566, Norse Focus Staff", "567, Briton Shod Quarterstaff",
                "568, Briton Focus Staff", "821, Necro Staff", "828, Claw Hand Staff", "881, Alb Bishops Reach Staff", "882, Alb Spiked Staff",
                "1166, Albion Mage Staff 01", "1167, Albion Mage Staff 02", "1168, Albion Mage Staff 03", "1169, Albion Mage Staff 04",
                "1170, Albion Mage Staff 05", "1171, Necro Staff 01", "1172, Necro Staff 02", "1173, Hib Staff 01", "1174, Hib Staff 02",
                "1175, Hib Staff 03", "1176, Hib Staff 04", "1177, Hib Staff 05", "1178, Hib Staff 06", "1179, Hib Staff 07",
                "1180, Hib Staff 08", "1181, Hib Staff 09", "1182, Hib Staff 10", "1183, Hib Staff 11", "1184, Hib Staff 12",
                "1185, Hib Staff 13", "1658, Staffoftheoracle 01", "1672, Sceptermeritorious 01", "1821, Bonedancer Staff",
                "1856, Norse Rod 1", "1857, Norse Rod 2", "1858, Norse Rod 3", "1859, Norse Wand 1", "1860, Norse Wand 2",
                "1861, Norse Wand 3", "1908, Lab Quarter Staff", "1909, Lab Mage Staff", "1950, Scorched Lab Quarter Staff",
                "1964, Scorched Lab Mage Staff", "1965, Albion Mage Staff 01 Air", "1966, Albion Mage Staff 01 Earth",
                "1967, Albion Mage Staff 01 Fire", "1968, Albion Mage Staff 01 Water", "1999, Dragonsworn Mage Staff",
                "2003, Dragonsworn Quarter Staff", "2017, Hib Staff 06 Air", "2018, Hib Staff 06 Earth", "2019, Hib Staff 06 Fire",
                "2020, Hib Staff 06 Water", "2060, Hib DragonSlayer Quarter Staff", "2064, Hib DragonSlayer Mage Staff",
                "2065, Mid Staff 04 Air", "2066, Mid Staff 04 Earth", "2067, Mid Staff 04 Fire", "2068, Mid Staff 04 Water",
                "2099, Mid DragonSlayer Quarter Staff", "2103, Mid DragonSlayer Mage Staff", "2134, Alb DragonSlayer Quarter Staff",
                "2138, Alb DragonSlayer Mage Staff", "2199, Toa_S_Bl_Symbolstaff01", "2319, Pict Quarter Staff", "2323, Pict Mage Staff",
                "3365, Alb Mauler Champion Staff", "3368, Admorenth Staff", "3548, Hib Mauler Champion Staff", "3565, Alb Mauler Champion Staff",
                "3567, Mid Mauler Champion Staff", "4458, Hib Mauler Champion Staff DF", "4460, Alb Mauler Champion Staff DF",
                "4462, Mid Mauler Champion Staff DF", "4503, Albion Mage Staff 01 Body", "4504, Albion Mage Staff 01 Energy",
                "4529, Hib Staff 06 Body", "4530, Hib Staff 06 Energy", "4553, Mid Staff 04 Body", "4554, Mid Staff 04 Energy",
                "4950, Wabbajack", "4951, Staff Infidus", "4952, Staff of Magnus", "4953, Staff Yuna", "4959, Chronoscepter"
            });

            // Flexible
            RegisterBatch(eObjectType.Flexible, eInventorySlot.RightHandWeapon, new[] {
                "857, Alb Flex Chain", "858, Alb Flex Chain Saw", "859, Alb Flex Chain Whip", "860, Alb Flex Dagger Flail",
                "861, Alb Flex Flail", "862, Alb Flex Morning Star", "863, Alb Flex Pick Flail", "864, Alb Flex Spiked Flail",
                "865, Alb Flex Spiked Whip", "866, Alb Flex War Chain", "867, Alb Flex Whip", "868, Alb Flex Whip Dagger",
                "869, Alb Flex Whip Mace", "1895, Lab Flex Thrust", "1896, Lab Flex Slash", "1897, Lab Flex Blunt",
                "1921, Alb Flex Spiked Flail Air", "1922, Alb Flex Spiked Flail Earth", "1923, Alb Flex Spiked Flail Fire",
                "1924, Alb Flex Spiked Flail Water", "1925, Alb Flex Spiked Whip Air", "1926, Alb Flex Spiked Whip Earth",
                "1927, Alb Flex Spiked Whip Fire", "1928, Alb Flex Spiked Whip Water", "1936, Scorched Lab Flex Thrust",
                "1937, Scorched Lab Flex Slash", "1938, Scorched Lab Flex Blunt", "1989, Dragonsworn Flex Thrust",
                "1990, Dragonsworn Flex Slash", "1991, Dragonsworn Flex Crush", "2072, Hib DragonSlayer Flex Thrust",
                "2073, Hib DragonSlayer Flex Slash", "2078, Mid DragonSlayer Flex Crush", "2111, Mid DragonSlayer Flex Thrust",
                "2112, Mid DragonSlayer Flex Slash", "2113, Alb DragonSlayer Flex Crush", "2119, Toa Afct Serpent Whip",
                "2146, Alb DragonSlayer Flex Thrust", "2147, Alb DragonSlayer Flex Slash", "2298, Pict Flex Crush",
                "2331, Pict Flex Thrust", "2332, Pict Flex Slash", "4481, Alb Flex Spiked Flail body", "4482, Alb Flex Spiked Flail Energy",
                "4483, Alb Flex Spiked Whip body", "4484, Alb Flex Spiked Whip Energy"
            });

            // Instruments
            RegisterBatch(eObjectType.Instrument, eInventorySlot.DistanceWeapon, new[] {
                "228, Drum", "2114, Toa_In_Drum", "2971, Base_drum", "2974, Old_drum", "2977, Albion_drum", "2980, Hibernia_drum",
                "227, Lute", "2117, Toa_In_Lute", "2970, Base_Lute", "2973, Old_Lute", "2976, Albion_Lute", "2979, Hibernia_Lute",
                "325, Flute", "2115, Toa_In_Flute", "2972, Base_flute", "2975, Old_flute", "2978, Albion_flute", "2981, Hibernia_flute",
                "1930, Lab Harp", "1965, Scorched Lab Harp", "1998, Dragonworn Mandolin", "2065, Hib Dragonslayer Harp",
                "2104, Mid Dragonslayer Harp", "2116, Toa_In_Harp", "2139, Alb Dragonslayer Harp", "2324, Pict Harp"
            });

            // Shields
            RegisterBatch(eObjectType.Shield, eInventorySlot.LeftHandWeapon, new[] {
                // Small
                "1040, Buckler", "1103, Buckler", "1118, Kite Buckler", "1130, Grave Buckler", "1139, Norse Buckler",
                "1148, Celtic Buckler", "1163, Leaf Buckler", "1912, Lab Small Shield", "1947, Scorched Lab Small Shield",
                "2007, Dragonsworn Small Shield", "2055, Hib DragonSlayer Small Shield", "2094, Mid DragonSlayer Small Shield",
                "2129, Alb DragonSlayer Small Shield", "2192, Nautilus Shield", "2200, Symbol Shield", "2210, Aerus Shield",
                "2218, Magma Shield", "2314, Pict Small Shield", "2725, Possessed Small Shield", "3828, Dragonsworn Small Shield",
                "3888, Hib DragonSlayer Small Shield", "3929, Mid DragonSlayer Small Shield", "3965, Alb DragonSlayer Small Shield",
                "4293, Pict Small Shield",
                // Medium
                "59, Round Wooden Shield", "60, Heater Shield",
                "1049, Heater", "1067, LHeater", "1076, LRound",
                "1094, MRound", "1109, Great Kite", "1112, Horned Shield", "1115, Kite Shield", "1124, Crescent Shield", "1127, Grave Shield",
                "1145, Celtic Shield", "1160, Leaf Point Shield", "1663, AtenShield", "1664, CyclopsEye", "1665, ShieldofKhaos",
                "2006, Dragonsworn Medium Shield", "2056, Hib DragonSlayer Medium Shield", "2095, Mid DragonSlayer Medium Shield",
                "2130, Alb DragonSlayer Medium Shield", "2201, Symbol Shield", "2211, Aerus Shield", "2219, Magma Shield",
                "2315, Pict Medium Shield", "2726, Possessed Medium Shield", "3829, Dragonsworn Medium Shield", "3889, Hib DragonSlayer Medium Shield",
                "3930, Mid DragonSlayer Medium Shield", "3966, Alb DragonSlayer Medium Shield", "4294, Pict Medium Shield", "4821, Egg shield",
                "4835, Heart Shield", "4956, Lionheart Shield", "4957, Rodric Shield", "4958, Hylian Shield",
                // Large
                "79, Tower Shield",
                "1058, LBlock", "1085, MBlock",
                "1106, Great Horned", "1121, Alb Tower Shield Leather", "1133, Great Crescent Shield", "1136, Great Grave Shield", "1142, Norse Tower Shield",
                "1151, Celtic Tower Shield", "1154, Great Celtic Shield", "1157, Great Leaf Shield", "1910, Lab XL Shield", "1911, Lab Large Shield",
                "1946, Scorched Lab XL Shield", "1948, Scorched Lab Large Shield", "2005, Dragonsworn Large Shield",
                "2057, Hib DragonSlayer Large Shield", "2096, Mid DragonSlayer Large Shield", "2131, Alb DragonSlayer Large Shield",
                "2202, Symbol Shield", "2212, Aerus Shield", "2220, Magma Shield", "2316, Pict Large Shield", "2727, Possessed Large Shield",
                "3493, Relic Artisan Shield", "3554, Clockwork MinoSkull Shield", "3830, Dragonsworn Large Shield",
                "3890, Hib DragonSlayer Large Shield", "3931, Mid DragonSlayer Large Shield", "3967, Alb DragonSlayer Large Shield",
                "4295, Pict Large Shield"
            });

            // ==========================================================
            // ARMOR
            // ==========================================================

            // Cloth
            RegisterArmorBatch(eObjectType.Cloth, new[] {
                 // Torso
                 "58, Robe 1", "65, Robe 2", "66, Robe 3", "97, Fancy Robe", "98, Fance Robe", "139, Cloth Vest", "151, Quilted vest",
                 "161, New Cloth 3 Vest", "166, New Cloth 4 Vest", "171, Special Cloth Vest", "245, Norse Cloth Vest", "265, Norse Cloth 2 Vest",
                 "285, Norse Cloth 3 Vest", "305, Norse Cloth Special Vest", "338, Celtic Cloth Special Vest", "358, Celtic Cloth Worn Vest",
                 "378, Celtic Cloth 1 Vest", "398, Celtic Cloth 2 Vest", "418, Celtic Cloth 3 Vest", "441, Friar Robe", "682, Albion Cabalist",
                 "683, Robe 0", "684, Robe 4", "685, Robe 5", "686, Friar Robe 2", "687, Friar Robe 3", "703, Midgard Runemaster Cloth Vest",
                 "733, Albion Theurgist", "798, Albion Wizard", "799, Midgard Spiritmaster Cloth Vest", "804, Albion Sorcerer",
                 "983, Norse Isles Cloth Vest", "993, Norse Isles Cloth 4 Vest", "1005, Briton Isles Robe", "1006, Briton Isles Robe",
                 "1007, Hibernia Isles Robe", "1008, Hibernia Isles Robe", "1300, Mid Rp Basic Set 1 Tunic", "1304, Mid Rp Basic Set 2 Tunic",
                 "1305, Mid Rp Basic Set 3 Tunic", "1309, Alb Rp Basic Set 1 Tunic", "1313, Alb Rp Basic Set 2 Tunic", "1314, Alb Rp Basic Set 3 Robe",
                 "1332, Hib Rp Basic Set 1 Tunic", "1336, Hib Rp Basic Set 2 Tunic", "1337, Hib Rp Basic Set 3 Tunic", "1619, Albion Oceanus Cloth Robe",
                 "1621, Midgard Oceanus Cloth Robe", "1623, Hibernia Oceanus Cloth Robe", "1626, Oceanus Cloth Tunic Alb", "1627, Oceanus Cloth Tunic Mid",
                 "1628, Oceanus ClothTunic Hib", "2153, Stygia Cloth Tunic Alb", "2154, Stygia Cloth Tunic Mid", "2155, Stygia ClothTunic Hib",
                 "2160, Stygia Cloth Robe", "2162, Volcanus Cloth Tunic Alb", "2163, Volcanus Cloth Tunic Mid", "2164, Volcanus ClothTunic Hib",
                 "2169, Volcanus Cloth Robe", "2170, Volcanus Cloth Robe", "2171, Volcanus Cloth Robe", "2172, Oceanus Cloth Robe",
                 "2173, Oceanus Cloth Robe", "2174, Oceanus Cloth Robe", "2221, Stygia Cloth Robe", "2222, Stygia Cloth Robe",
                 "2230, Researcher Cloth Robe", "2231, Researcher Cloth Robe", "2232, Researcher Cloth Tunic", "2238, Aerus Cloth Tunic",
                 "2239, Aerus Cloth Tunic", "2240, Aerus ClothTunic", "2245, Aerus Cloth Robe", "2246, Aerus Cloth Robe", "2247, Aerus Cloth Tunic",
                 "2470, ToA Aerus Gift Tunic", "2516, Stygia Naliah Robe", "2694, Possessed Midgard cloth chest", "2695, Possessed Midgard cloth robe",
                 "2728, Possessed Albion cloth chest", "2729, Possessed Albion cloth robe", "2759, Possessed Hibernia cloth chest",
                 "2760, Possessed Hibernia cloth robe", "2790, Good Albion cloth chest", "2791, Good Albion cloth robe",
                 "2821, Good Hibernia cloth chest", "2822, Good Hibernia cloth robe", "2852, Good Midgard cloth chest", "2853, Good Midgard cloth robe",
                 "3017, Good Shar cloth chest", "3018, Good Shar cloth robe", "3058, Good Inconnu cloth chest", "3059, Good Inconnu cloth robe",
                 "3069, Possessed Inconnu cloth chest", "3080, Possessed Shar cloth chest", "3081, Possessed Shar cloth robe",
                 "3121, Poss Inconnu Hib cloth chest", "3141, Poss Inconnu Mid cloth chest", "3161, Possessed Shar Alb cloth chest",
                 "3162, Possessed Shar Alb cloth robe", "3187, Possessed Shar Mid cloth chest", "3188, Possessed Shar Mid cloth robe",
                 "3209, Merchant Cloth 1 Vest", "3214, Merchant Cloth 2 Vest", "3219, Merchant Cloth 3 Vest", "3605, Corrupt Robe", "3631, Mino Robe",
                 "3651, Mino Cloth Vest", "3652, Corrupt Cloth Vest", "3694, Midgard Mino Caster Vest", "3695, Midgard Corrupt Caster Vest",
                 "3783, Dragonsworn Robe", "3788, Dragonsworn Cloth Vest", "3793, Dragonsworn Midgard Robe", "3794, Robe 1 - Midgard Version",
                 "3795, Robe 2 - Midgard Version", "3796, Robe 3 - Midgard Version", "3797, Robe 0 - Midgard Version", "3798, Robe 4 - Midgard Version",
                 "3799, Robe 5 - Midgard Version", "4015, Alb Dragonslayer Robe", "4020, Alb Dragonslayer Cloth Vest", "4046, Mid Dragonslayer Robe",
                 "4051, Mid Dragonslayer Cloth Vest", "4052, Mid Dragonslayer Half-Robe", "4099, Hib Dragonslayer Robe", "4104, Hib Dragonslayer Cloth Vest",
                 // Legs
                 "140, Cloth Legs", "152, Quilted Legs", "162, New Cloth 3 Legs", "167, New Cloth 4 Legs", "172, Special Cloth Legs",
                 "246, Norse Cloth Legs", "266, Norse Cloth 2 Legs", "286, Norse Cloth 3 Legs", "306, Norse Cloth Special Legs",
                 "339, Celtic Cloth Special Legs", "359, Celtic Cloth Worn Legs", "379, Celtic Cloth 1 Legs", "399, Celtic Cloth 2 Legs",
                 "419, Celtic Cloth 3 Legs", "704, Midgard Runemaster Cloth Legs", "800, Midgard Spiritmaster Cloth Legs", "984, Norse Isles Cloth Legs",
                 "994, Norse Isles Cloth 4 Legs", "1303, Mid Rp Basic Set 1 Legs", "1312, Alb Rp Basic Set 1 Legs", "1335, Hib Rp Basic Set 1 Legs",
                 "1631, Oceanus Cloth Legs", "1632, Oceanus Cloth Legs", "2158, Stygia Cloth Legs", "2159, Stygia Cloth Legs",
                 "2167, Volcanus Cloth Legs", "2168, Volcanus Cloth Legs", "2234, Researcher Cloth Legs", "2243, Aerus Cloth Legs",
                 "2244, Aerus Cloth Legs", "2504, Alvarus Legs", "2505, Alvarus Legs", "2696, Possessed Midgard cloth pants",
                 "2730, Possessed Albion cloth pants", "2761, Possessed Hibernia cloth pants", "2792, Good Albion cloth pants",
                 "2823, Good Hibernia cloth pants", "2854, Good Midgard cloth pants", "3019, Good Shar cloth pants", "3060, Good Inconnu cloth pants",
                 "3071, Possessed Inconnu cloth pants", "3082, Possessed Shar cloth pants", "3122, Poss Inconnu Hib cloth pants",
                 "3142, Poss Inconnu Mid cloth pants", "3163, Possessed Shar Alb cloth pants", "3189, Possessed Shar Mid cloth pants",
                 "3210, Merchant Cloth 1 Legs", "3215, Merchant Cloth 2 Legs", "3220, Merchant Cloth 3 Legs", "3643, Mino Cloth Legs",
                 "3647, Corrupt Cloth Legs", "3784, Dragonsworn Cloth Legs", "4016, Alb Dragonslayer Cloth Legs", "4047, Mid Dragonslayer Cloth Legs",
                 "4100, Hib Dragonslayer Cloth Legs",
                 // Arms
                 "141, Cloth Arms", "153, Quilted Arms", "163, New Cloth 3 Arms", "168, New Cloth 4 Arms", "173, Special Cloth Arms",
                 "247, Norse Cloth Arms", "267, Norse Cloth 2 Arms", "287, Norse Cloth 3 Arms", "307, Norse Cloth Special Arms",
                 "340, Celtic Cloth Special Arms", "360, Celtic Cloth Worn Arms", "380, Celtic Cloth 1 Arms", "400, Celtic Cloth 2 Arms",
                 "420, Celtic Cloth 3 Arms", "705, Midgard Runemaster Cloth Arms", "801, Midgard Spiritmaster Cloth Arms",
                 "985, Norse Isles Cloth Arms", "995, Norse Isles Cloth 4 Arms", "1625, Oceanus Cloth Arms", "2152, Stygia Cloth Arms",
                 "2161, Volcanus Cloth Arms", "2229, Researcher Cloth Arms", "2233, Researcher Cloth Arms", "2237, Aerus Cloth Arms",
                 "2697, Possessed Midgard cloth sleeves", "2731, Possessed Albion cloth sleeves", "2762, Possessed Hibernia cloth sleeves",
                 "2793, Good Albion cloth sleeves", "2824, Good Hibernia cloth sleeves", "2855, Good Midgard cloth sleeves",
                 "3020, Good Shar cloth sleeves", "3061, Good Inconnu cloth sleeves", "3072, Possessed Inconnu cloth sleeves",
                 "3083, Possessed Shar cloth sleeves", "3123, Poss Inconnu Hib cloth sleeves", "3143, Poss Inconnu Mid cloth sleeves",
                 "3164, Possessed Shar Alb cloth sleeves", "3190, Possessed Shar Mid cloth sleeves", "3211, Merchant Cloth 1 Arms",
                 "3216, Merchant Cloth 2 Arms", "3221, Merchant Cloth 3 Arms", "3644, Mino Cloth Arms", "3648, Corrupt Cloth Arms",
                 "3785, Dragonsworn Cloth Arms", "4017, Alb Dragonslayer Cloth Arms", "4048, Mid Dragonslayer Cloth Arms",
                 "4101, Hib Dragonslayer Cloth Arms",
                 // Hands
                 "142, Cloth Gloves", "154, Quilted Gloves", "164, New Cloth 3 Gloves", "169, New Cloth 4 Gloves", "174, Special Cloth Gloves",
                 "248, Norse Cloth Gloves", "268, Norse Cloth 2 Gloves", "288, Norse Cloth 3 Gloves", "308, Norse Cloth Special Gloves",
                 "341, Celtic Cloth Special Gloves", "361, Celtic Cloth Worn Gloves", "381, Celtic Cloth 1 Gloves", "401, Celtic Cloth 2 Gloves",
                 "421, Celtic Cloth 3 Gloves", "706, Midgard Runemaster Cloth Gloves", "802, Midgard Spiritmaster Cloth Gloves",
                 "986, Norse Isles Cloth Gloves", "996, Norse Isles Cloth 4 Gloves", "1302, Mid Rp Basic Set 1 Gloves", "1311, Alb Rp Basic Set 1 Gloves",
                 "1334, Hib Rp Basic Set 1 Gloves", "1620, Albion Oceanus Cloth Gloves", "1622, Midgard Oceanus Cloth Gloves",
                 "1624, Hibernia Oceanus Cloth Gloves", "2235, Researcher Cloth Gloves", "2248, Stygia Cloth Glove", "2249, Volcanus Cloth Glove",
                 "2250, Aerus Cloth Glove", "2700, Possessed Midgard cloth gloves", "2734, Possessed Albion cloth gloves",
                 "2765, Possessed Hibernia cloth gloves", "2796, Good Albion cloth gloves", "2827, Good Hibernia cloth gloves",
                 "2858, Good Midgard cloth gloves", "3022, Good Shar cloth gloves", "3063, Good Inconnu cloth gloves",
                 "3074, Possessed Inconnu cloth gloves", "3085, Possessed Shar cloth gloves", "3125, Poss Inconnu Hib cloth gloves",
                 "3145, Poss Inconnu Mid cloth gloves", "3166, Possessed Shar Alb cloth gloves", "3192, Possessed Shar Mid cloth gloves",
                 "3212, Merchant Cloth 1 Gloves", "3217, Merchant Cloth 2 Gloves", "3645, Mino Cloth Gloves", "3649, Corrupt Cloth Gloves",
                 "3786, Dragonsworn Cloth Gloves", "4018, Alb Dragonslayer Cloth Gloves", "4049, Mid Dragonslayer Cloth Gloves",
                 "4102, Hib Dragonslayer Cloth Gloves",
                 // Feet
                 "143, Cloth Boots", "155, Quilted Boots", "165, New Cloth 3 Boots", "170, New Cloth 4 Boots", "175, Special Cloth Boots",
                 "249, Norse Cloth Boots", "269, Norse Cloth 2 Boots", "289, Norse Cloth 3 Boots", "309, Norse Cloth Special Boots",
                 "342, Celtic Cloth Special Boots", "362, Celtic Cloth Worn Boots", "382, Celtic Cloth 1 Boots", "402, Celtic Cloth 2 Boots",
                 "422, Celtic Cloth 3 Boots", "707, Midgard Runemaster Cloth Boots", "803, Midgard Spiritmaster Cloth Boots",
                 "987, Norse Isles Cloth Boots", "997, Norse Isles Cloth 4 Boots", "1301, Mid Rp Basic Set 1 Boots", "1310, Alb Rp Basic Set 1 Boots",
                 "1333, Hib Rp Basic Set 1 Boots", "1629, Oceanus Cloth Boots", "1630, Oceanus Cloth Boots", "2156, Stygia Cloth Boots",
                 "2157, Stygia Cloth Boots", "2165, Volcanus Cloth Boots", "2166, Volcanus Cloth Boots", "2236, Researcher Cloth Boots",
                 "2241, Aerus Cloth Boots", "2242, Aerus Cloth Boots", "2699, Possessed Midgard cloth boots", "2733, Possessed Albion cloth boots",
                 "2764, Possessed Hibernia cloth boots", "2795, Good Albion cloth boots", "2826, Good Hibernia cloth boots",
                 "2857, Good Midgard cloth boots", "3021, Good Shar cloth boots", "3062, Good Inconnu cloth boots", "3073, Possessed Inconnu cloth boots",
                 "3084, Possessed Shar cloth boots", "3124, Poss Inconnu Hib cloth boots", "3144, Poss Inconnu Mid cloth boots",
                 "3165, Possessed Shar Alb cloth boots", "3191, Possessed Shar Mid cloth boots", "3213, Merchant Cloth 1 Boots",
                 "3218, Merchant Cloth 2 Boots", "3222, Merchant Cloth 3 Boots", "3646, Mino Cloth Boots", "3650, Corrupt Cloth Boots",
                 "3787, Dragonsworn Cloth Boots", "4019, Alb Dragonslayer Cloth Boots", "4050, Mid Dragonslayer Cloth Boots",
                 "4103, Hib Dragonslayer Cloth Boots",
                 // Head
                 "822, Albion Cloth Cap", "823, Albion Cloth Helm", "825, Norse Cloth Cap", "826, Celtic Cloth Cap 1", "1197, Celtic Cloth Helm 2",
                 "1213, Norse Cloth Helm 2", "1229, Albion Cloth Helm 1", "1230, Albion Cloth Helm 2", "2251, Oceanus Cloth Helm",
                 "2252, OceanusCloth Helm", "2253, Oceanus Cloth Helm", "2269, Oceanus Cloth Helm", "2270, OceanusCloth Helm",
                 "2271, Oceanus Cloth Helm", "2287, Oceanus Cloth Helm", "2288, OceanusCloth Helm", "2289, Oceanus Cloth Helm",
                 "2305, Stygia Cloth Helm", "2306, StygiaCloth Helm", "2307, Stygia Cloth Helm", "2323, Stygia Cloth Helm",
                 "2324, StygiaCloth Helm", "2325, Stygia Cloth Helm", "2341, Stygia Cloth Helm", "2342, StygiaCloth Helm",
                 "2343, Stygia Cloth Helm", "2359, Volcanus Cloth Helm", "2360, VolcanusCloth Helm", "2361, Volcanus Cloth Helm",
                 "2377, Volcanus Cloth Helm", "2378, VolcanusCloth Helm", "2379, Volcanus Cloth Helm", "2395, Volcanus Cloth Helm",
                 "2396, VolcanusCloth Helm", "2397, Volcanus Cloth Helm", "2413, Aerus Cloth Helm", "2414, AerusCloth Helm",
                 "2415, Aerus Cloth Helm", "2431, Aerus Cloth Helm", "2432, AerusCloth Helm", "2433, Aerus Cloth Helm",
                 "2449, Aerus Cloth Helm", "2450, AerusCloth Helm", "2451, Aerus Cloth Helm", "4063, Hib DragonSlayer Cloth Full Helm",
                 "4070, Mid DragonSlayer Cloth Full Helm"
            });

            // Leather
            RegisterArmorBatch(eObjectType.Leather, new[] {
                "31, Leather Tunic", "36, Hard Leather Vest", "74, Rein. Leather Vest", "134, Patterned Leather Vest", "146, Crude Leather Vest",
                "176, Special Leather Vest", "240, Norse Leather Vest", "260, Norse Leather 2 Vest", "280, Norse Leather 3 Vest",
                "300, Norse Leather Special Vest", "353, Celtic Leather Special Vest", "373, Celtic Leather Worn Vest", "393, Celtic Leather 1 Vest",
                "413, Celtic Leather 2 Vest", "433, Celtic Leather 3 Vest", "728, Albion Scout Vest", "734, Hibernian Bard Vest",
                "739, Hibernian Druid Vest", "746, Hibernian Nightshade Vest", "751, Midgard Berserker Vest", "756, Midgard Hunter Vest",
                "761, Midgard Shadowblade Vest", "782, Hibernian Blademaster Vest", "792, Albion Infiltrator Vest", "805, Hibernian Warden Vest",
                "815, Hibernian Ranger Vest", "1192, Savage Epic Vest", "1640, Oceanus Leather Tunic", "2135, Stygia Leather Body",
                "2144, Aerus Leather Body", "2470, Aerus Gift Tunic", "2499, Stygia Naliah Chest", "2512, Aerus Leather Robe",
                "2513, Volcanus Leather Robe", "2514, Oceanus Leather Robe", "2515, Stygia Leather Robe", "2518, Stygia Naliah Robe",
                "2701, Possessed Midgard leather chest", "2735, Possessed Albion leather chest", "2766, Possessed Hibernia leather chest",
                "2797, Good Albion leather chest", "2828, Good Hibernia leather chest", "2859, Good Midgard leather chest",
                "2988, Good Shar leather chest", "3064, Good Inconnu leather chest", "3075, Possessed Inconnu leather chest",
                "3086, Possessed Shar leather chest", "3126, Poss Inconnu Hib leather chest", "3146, Poss Inconnu Mid leather chest",
                "3167, Possessed Shar Alb leather chest", "3193, Possessed Shar Mid leather chest", "3375, Midgard Berserker Vest",
                "3560, Hib Mauler Epic Vest", "3570, Mid Mauler Epic Vest", "3580, Corrupt Leather Vest", "3606, Mino Leather Vest",
                "3638, Alb Mauler Epic Vest", "3758, Dragonsworn Leather Vest", "3990, Alb Dragonslayer Leather Vest",
                "4021, Mid Dragonslayer Leather Vest", "4074, Hib Dragonslayer Leather Vest",
                // Legs
                "32, Leather Leggings", "37, Hard Leather Legs", "75, Reinf. Leather Legs", "135, Patterned Leather Legs", "147, Crude Leather Legs",
                "177, Special Leather Legs", "241, Norse Leather Legs", "261, Norse Leather 2 Legs", "281, Norse Leather 3 Legs",
                "301, Norse Leather Special Legs", "354, Celtic Leather Special Legs", "374, Celtic Leather Worn Legs", "394, Celtic Leather 1 Legs",
                "414, Celtic Leather 2 Legs", "434, Celtic Leather 3 Legs", "729, Albion Scout Legs", "735, Hibernian Bard Legs",
                "740, Hibernian Druid Legs", "747, Hibernian Nightshade Legs", "752, Midgard Berserker Legs", "757, Midgard Hunter Legs",
                "762, Midgard Shadowblade Legs", "783, Hibernian Blademaster Legs", "793, Albion Infiltrator Legs", "806, Hibernian Warden Legs",
                "816, Hibernian Ranger Legs", "1193, Savage Epic Legs", "1646, Oceanus Leather Legs", "1647, Oceanus Leather Legs",
                "2141, Stygia Leather Legs", "2142, Stygia Leather Legs", "2150, Aerus Leather Legs", "2151, Aerus Leather Legs",
                "2506, Alvarus Legs", "2507, Alvarus Legs", "2702, Possessed Midgard leather pants", "2736, Possessed Albion leather pants",
                "2767, Possessed Hibernia leather pants", "2798, Good Albion leather pants", "2829, Good Hibernia leather pants",
                "2860, Good Midgard leather pants", "2989, Good Shar leather pants", "3065, Good Inconnu leather pants",
                "3076, Possessed Inconnu leather pants", "3087, Possessed Shar leather pants", "3127, Poss Inconnu Hib leather pants",
                "3147, Poss Inconnu Mid leather pants", "3168, Possessed Shar Alb leather pants", "3194, Possessed Shar Mid leather pants",
                "3363, Alb Mauler Epic Legs", "3376, Midgard Berserker Legs", "3561, Hib Mauler Epic Legs", "3571, Mid Mauler Epic Legs",
                "3581, Corrupt Leather Legs", "3607, Mino Leather Legs", "3639, Alb Mauler Epic Legs", "3759, Dragonsworn Leather Legs",
                "3991, Alb Dragonslayer Leather Legs", "4022, Mid Dragonslayer Leather Legs", "4075, Hib Dragonslayer Leather Legs",
                // Arms
                "33, Leather Arms", "38, Hard Leather Arms", "76, Reinf. Leather Arms", "120, Leather Shoulder Armor", "121, Leather Bracers",
                "136, Patterned Leather Arms", "148, Crude Leather Arms", "178, Special Leather Arms", "242, Norse Leather Arms",
                "262, Norse Leather 2 Arms", "282, Norse Leather 3 Arms", "302, Norse Leather Special Arms", "355, Celtic Leather Special Arms",
                "375, Celtic Leather Worn Arms", "395, Celtic Leather 1 Arms", "415, Celtic Leather 2 Arms", "435, Celtic Leather 3 Arms",
                "730, Albion Scout Arms", "736, Hibernian Bard Arms", "741, Hibernian Druid Arms", "748, Hibernian Nightshade Arms",
                "753, Midgard Berserker Arms", "758, Midgard Hunter Arms", "763, Midgard Shadowblade Arms", "784, Hibernian Blademaster Arms",
                "794, Albion Infiltrator Arms", "807, Hibernian Warden Arms", "817, Hibernian Ranger Arms", "1194, Savage Epic Arms",
                "1639, Oceanus Leather Arm", "2134, Stygia Leather Arms", "2143, Aerus Leather Arms", "2501, TOA Oceanus winds arm Leather",
                "2703, Possessed Midgard leather sleeves", "2737, Possessed Albion leather sleeves", "2768, Possessed Hibernia leather sleeves",
                "2799, Good Albion leather sleeves", "2830, Good Hibernia leather sleeves", "2861, Good Midgard leather sleeves",
                "2990, Good Shar leather sleeves", "3066, Good Inconnu leather sleeves", "3077, Possessed Inconnu leather sleeves",
                "3088, Possessed Shar leather sleeves", "3128, Poss Inconnu Hib leather sleeves", "3148, Poss Inconnu Mid leather sleeves",
                "3169, Possessed Shar Alb leather sleeves", "3195, Possessed Shar Mid leather sleeves", "3364, Alb Mauler Epic Arms",
                "3377, Midgard Berserker Arms", "3562, Hib Mauler Epic Arms", "3572, Mid Mauler Epic Arms", "3582, Corrupt Leather Arms",
                "3608, Mino Leather Arms", "3640, Alb Mauler Epic Arms", "3760, Dragonsworn Leather Arms", "3992, Alb Dragonslayer Leather Arms",
                "4023, Mid Dragonslayer Leather Arms", "4076, Hib Dragonslayer Leather Arms",
                // Hands
                "34, Leather Gloves", "39, Hard Leather Gloves", "77, Reinf. Leather Gloves", "137, Patterned Leather Gloves", "149, Crude Leather Gloves",
                "179, Special Leather Gloves", "243, Norse Leather Gloves", "263, Norse Leather 2 Gloves", "283, Norse Leather 3 Gloves",
                "303, Norse Leather Special Gloves", "356, Celtic Leather Special Gloves", "376, Celtic Leather Worn Gloves", "396, Celtic Leather 1 Gloves",
                "416, Celtic Leather 2 Gloves", "436, Celtic Leather 3 Gloves", "732, Albion Scout Gloves", "737, Hibernian Bard Gloves",
                "742, Hibernian Druid Gloves", "749, Hibernian Nightshade Gloves", "754, Midgard Berserker Gloves", "759, Midgard Hunter Gloves",
                "764, Midgard Shadowblade Gloves", "785, Hibernian Blademaster Gloves", "795, Albion Infiltrator Gloves", "808, Hibernian Warden Gloves",
                "818, Hibernian Ranger Gloves", "1195, Savage Epic Gloves", "1645, Oceanus Leather Gloves", "2140, Stygia Leather Glove",
                "2149, Aerus Leather Glove", "2706, Possessed Midgard leather gloves", "2740, Possessed Albion leather gloves",
                "2771, Possessed Hibernia leather gloves", "2802, Good Albion leather gloves", "2833, Good Hibernia leather gloves",
                "2864, Good Midgard leather gloves", "2993, Good Shar leather gloves", "3068, Good Inconnu leather gloves",
                "3079, Possessed Inconnu leather gloves", "3090, Possessed Shar leather gloves", "3130, Poss Inconnu Hib leather gloves",
                "3150, Poss Inconnu Mid leather gloves", "3171, Possessed Shar Alb leather gloves", "3197, Possessed Shar Mid leather gloves",
                "3378, Midgard Berserker Gloves", "3563, Hib Mauler Epic Gloves", "3573, Mid Mauler Epic Gloves", "3583, Corrupt Leather Gloves",
                "3609, Mino Leather Gloves", "3641, Alb Mauler Epic Gloves", "3761, Dragonsworn Leather Gloves", "3993, Alb Dragonslayer Leather Gloves",
                "4024, Mid Dragonslayer Leather Gloves", "4077, Hib Dragonslayer Leather Gloves",
                // Feet
                "40, Hard Leather Boots", "78, Reinf. Leather Boots", "133, Leather Boots", "138, Patterned Leather Boots", "150, Crude Leather Boots",
                "180, Special Leather Boots", "244, Norse Leather Boots", "264, Norse Leather 2 Boots", "284, Norse Leather 3 Boots",
                "304, Norse Leather Special Boots", "357, Celtic Leather Special Boots", "377, Celtic Leather Worn Boots", "397, Celtic Leather 1 Boots",
                "417, Celtic Leather 2 Boots", "437, Celtic Leather 3 Boots", "731, Albion Scout Boots", "738, Hibernian Bard Boots",
                "743, Hibernian Druid Boots", "750, Hibernian Nightshade Boots", "755, Midgard Berserker Boots", "760, Midgard Hunter Boots",
                "765, Midgard Shadowblade Boots", "786, Hibernian Blademaster Boots", "796, Albion Infiltrator Boots", "809, Hibernian Warden Boots",
                "819, Hibernian Ranger Boots", "1196, Savage Epic Boots", "1643, Oceanus Leather Boot Alb", "1644, Oceanus Leather Boot Mid/Hib",
                "2138, Stygia Leather Boots", "2139, Stygia Leather Boots", "2147, Aerus Leather Boots", "2148, Aerus Leather Boots",
                "2705, Possessed Midgard leather boots", "2739, Possessed Albion leather boots", "2770, Possessed Hibernia leather boots",
                "2801, Good Albion leather boots", "2832, Good Hibernia leather boots", "2863, Good Midgard leather boots", "2992, Good Shar leather boots",
                "3067, Good Inconnu leather boots", "3078, Possessed Inconnu leather boots", "3089, Possessed Shar leather boots",
                "3129, Poss Inconnu Hib leather boots", "3149, Poss Inconnu Mid leather boots", "3170, Possessed Shar Alb leather boots",
                "3196, Possessed Shar Mid leather boots", "3379, Midgard Berserker Boots", "3564, Hib Mauler Epic Boots", "3574, Mid Mauler Epic Boots",
                "3584, Corrupt Leather Boots", "3610, Mino Leather Boots", "3642, Alb Mauler Epic Boots", "3762, Dragonsworn Leather Boots",
                "3994, Alb Dragonslayer Leather Boots", "4025, Mid Dragonslayer Leather Boots", "4078, Hib Dragonslayer Leather Boots",
                // Head
                "1, Full Helmet Leather", "35, Leather Cap", "62, Albion Leather Helm 1", "122, Leather Neck Armor", "335, Norse Leather Helm 1",
                "336, Norse Leather Helm 2", "337, Norse Leather Helm 3", "438, Celtic Leather Helm 1", "439, Celtic Leather Helm 2",
                "440, Celtic Leather Helm 3", "491, Full Helmet Leather", "1198, Celtic Leather Helm 4", "1214, Norse Leather Helm 4",
                "1231, Albion Leather Helm 3", "1232, Albion Leather Helm 5", "2254, TOA Oceanus Leather Helm", "2255, TOA Oceanus Leather Helm",
                "2256, TOA Oceanus Leather Helm", "2272, TOA Oceanus Leather Helm", "2273, TOA Oceanus Leather Helm", "2274, TOA Oceanus Leather Helm",
                "2290, TOA Oceanus Leather Helm", "2291, TOA Oceanus Leather Helm", "2292, TOA Oceanus Leather Helm", "2308, TOA Stygia Leather Helm",
                "2309, TOA Stygia Leather Helm", "2310, TOA Stygia Leather Helm", "2326, TOA Stygia Leather Helm", "2327, TOA Stygia Leather Helm",
                "2328, TOA Stygia Leather Helm", "2344, TOA Stygia Leather Helm", "2345, TOA Stygia Leather Helm", "2346, TOA Stygia Leather Helm",
                "2362, TOA Volcanus Leather Helm", "2363, TOA Volcanus Leather Helm", "2364, TOA Volcanus Leather Helm",
                "2380, TOA Volcanus Leather Helm", "2381, TOA Volcanus Leather Helm", "2382, TOA Volcanus Leather Helm",
                "2398, TOA Volcanus Leather Helm", "2399, TOA Volcanus Leather Helm", "2400, TOA Volcanus Leather Helm",
                "2416, TOA Aerus Leather Helm", "2417, TOA Aerus Leather Helm", "2418, TOA Aerus Leather Helm", "2434, TOA Aerus Leather Helm",
                "2435, TOA Aerus Leather Helm", "2436, TOA Aerus Leather Helm", "2452, TOA Aerus Leather Helm", "2453, TOA Aerus Leather Helm",
                "2454, TOA Aerus Leather Helm", "4054, Albion DragonSlayer Leather Full Helm", "4061, Hib DragonSlayer Leather Full Helm",
                "4068, Mid DragonSlayer Leather Full Helm"
            });

            // Studded
            RegisterArmorBatch(eObjectType.Studded, new[] {
                "81, Studded Studs Vest", "156, Heavy Studded Vest", "216, Studded 4 Vest", "221, Special Studded Vest", "230, Norse Studded Vest",
                "250, Norse Studded 2 Vest", "270, Norse Studded 3 Vest", "290, Norse Studded Special Vest", "343, Celtic Reinforced Special Vest",
                "363, Celtic Reinforced Worn Vest", "383, Celtic Reinforced 1 Vest", "403, Celtic Reinforced 2 Vest", "423, Celtic Reinforced 3 Vest",
                "1256, Hib Reinforced Leather Vest", "1757, Stygia Studded Body", "1758, Stygia Studded Body", "1759, Stygia Studded Body",
                "1798, Aerus Studded Body", "1799, Aerus Studded Body", "1800, Aerus Studded Body", "1848, Oceanus Studded Body",
                "1849, Oceanus Studded Body", "1850, Oceanus Studded Body", "2473, ToA Aerus Gift Tunic", "2474, ToA Aerus Gift Tunic",
                "2475, ToA Aerus Gift Tunic", "2496, Stygia Scarab Chest", "2497, Stygia Scarab Chest", "2498, Stygia Scarab Chest",
                "2707, Possessed Midgard studded chest", "2741, Possessed Albion studded chest", "2772, Possessed Hibernia studded chest",
                "2803, Good Albion studded chest", "2834, Good Hibernia studded chest", "2865, Good Midgard studded chest",
                "3012, Good Shar studded chest", "3091, Possessed Shar reinforced chest", "3111, Possessed Inconnu studded chest",
                "3116, Good Inconnu studded chest", "3131, Poss Inconnu Hib studded chest", "3151, Poss Inconnu Mid studded chest",
                "3172, Possessed Shar Alb studded chest", "3198, Possessed Shar Mid studded chest", "3600, Corrupt studded Vest",
                "3626, Mino studded Vest", "3778, Dragonsworn studded Vest", "4010, Alb Dragonslayer studded Vest",
                "4041, Mid Dragonslayer studded Vest", "4094, Hib Dragonslayer studded Vest",
                // Legs
                "52, riveted studded legs", "82, Studded Studs Legs", "157, Heavy Studded Legs", "217, Studded 4 Legs", "222, Special Studded Legs",
                "231, Norse Studded Legs", "251, Norse Studded 2 Legs", "271, Norse Studded 3 Legs", "291, Norse Studded Special Legs",
                "344, Celtic Reinforced Special Legs", "364, Celtic Reinforced Worn Legs", "384, Celtic Reinforced 1 Legs",
                "404, Celtic Reinforced 2 Legs", "424, Celtic Reinforced 3 Legs", "1257, Hib Reinforced Leather Legs", "1763, Stygia Studded Leg",
                "1764, Stygia Studded Leg", "1804, Aerus Studded Leg", "1805, Aerus Studded Leg", "1854, Oceanus Studded Leg",
                "1855, Oceanus Studded Leg", "2508, Alvarus Legs", "2509, Alvarus Legs", "2708, Possessed Midgard studded pants",
                "2742, Possessed Albion studded pants", "2773, Possessed Hibernia studded pants", "2804, Good Albion studded pants",
                "2835, Good Hibernia studded pants", "2866, Good Midgard studded pants", "3013, Good Shar studded pants",
                "3092, Possessed Shar reinforced pants", "3112, Possessed Inconnu studded pants", "3117, Good Inconnu studded pants",
                "3132, Poss Inconnu Hib studded pants", "3152, Poss Inconnu Mid studded pants", "3173, Possessed Shar Alb studded pants",
                "3199, Possessed Shar Mid studded pants", "3601, Corrupt studded Legs", "3627, Mino studded Legs", "3779, Dragonsworn studded Legs",
                "4011, Alb Dragonslayer studded Legs", "4042, Mid Dragonslayer studded Legs", "4095, Hib Dragonslayer studded Legs",
                // Arms
                "53, riveted studded arms", "83, Studded Studs Arms", "123, Studded Shoulder Armor", "124, Studded Bracers",
                "158, Heavy Studded Arms", "218, Studded 4 Arms", "223, Special Studded Arms", "232, Norse Studded Arms",
                "252, Norse Studded 2 Arms", "272, Norse Studded 3 Arms", "292, Norse Studded Special Arms", "345, Celtic Reinforced Special Arms",
                "365, Celtic Reinforced Worn Arms", "385, Celtic Reinforced 1 Arms", "405, Celtic Reinforced 2 Arms", "425, Celtic Reinforced 3 Arms",
                "1258, Hib Reinforced Leather Arms", "1756, Stygia Studded Arm", "1797, Aerus Studded Arm", "1847, Oceanus Studded Arm",
                "2502, TOA Oceanus winds arm Studded", "2709, Possessed Midgard studded sleeves", "2743, Possessed Albion studded sleeves",
                "2774, Possessed Hibernia studded sleeves", "2805, Good Albion studded sleeves", "2836, Good Hibernia studded sleeves",
                "2867, Good Midgard studded sleeves", "3014, Good Shar studded sleeves", "3093, Possessed Shar reinforced sleeves",
                "3113, Possessed Inconnu studded sleeves", "3118, Good Inconnu studded sleeves", "3133, Poss Inconnu Hib studded sleeves",
                "3153, Poss Inconnu Mid studded sleeves", "3174, Possessed Shar Alb studded sleeves", "3200, Possessed Shar Mid studded sleeves",
                "3602, Corrupt studded Arms", "3628, Mino studded Arms", "3780, Dragonsworn studded Arms", "4012, Alb Dragonslayer studded Arms",
                "4043, Mid Dragonslayer studded Arms", "4096, Hib Dragonslayer studded Arms",
                // Hands
                "80, riveted studded gloves", "85, Studded Studs Gloves", "159, Heavy Studded Gloves", "219, Studded 4 Gloves",
                "224, Special Studded Gloves", "233, Norse Studded Gloves", "253, Norse Studded 2 Gloves", "273, Norse Studded 3 Gloves",
                "293, Norse Studded Special Gloves", "346, Celtic Reinforced Special Gloves", "366, Celtic Reinforced Worn Gloves",
                "386, Celtic Reinforced 1 Gloves", "406, Celtic Reinforced 2 Gloves", "426, Celtic Reinforced 3 Gloves",
                "1259, Hib Reinforced Leather Gloves", "1762, Stygia Studded Gloves", "1803, Aerus Studded Gloves", "1853, Oceanus Studded Gloves",
                "2712, Possessed Midgard studded gauntlets", "2746, Possessed Albion studded gauntlets", "2777, Possessed Hibernia studded gauntlets",
                "2808, Good Albion studded gauntlets", "2839, Good Hibernia studded gauntlets", "2870, Good Midgard studded gauntlets",
                "3016, Good Shar studded gauntlets", "3095, Possessed Shar reinforced gauntlets", "3115, Possessed Inconnu studded gauntlets",
                "3120, Good Inconnu studded gauntlets", "3135, Poss Inconnu Hib studded gauntlets", "3155, Poss Inconnu Mid studded gauntlets",
                "3176, Possessed Shar Alb studded gauntlets", "3202, Possessed Shar Mid studded gauntlets", "3604, Corrupt studded Gloves",
                "3630, Mino studded Gloves", "3782, Dragonsworn studded Gloves", "4014, Alb Dragonslayer studded Gloves",
                "4045, Mid Dragonslayer studded Gloves", "4098, Hib Dragonslayer studded Gloves",
                // Feet
                "54, riveted studded boots", "84, Studded Studs Boots", "160, Heavy Studded Boots", "220, Studded 4 Boots",
                "225, Special Studded Boots", "234, Norse Studded Boots", "254, Norse Studded 2 Boots", "274, Norse Studded 3 Boots",
                "294, Norse Studded Special Boots", "347, Celtic Reinforced Special Boots", "367, Celtic Reinforced Worn Boots",
                "387, Celtic Reinforced 1 Boots", "407, Celtic Reinforced 2 Boots", "427, Celtic Reinforced 3 Boots",
                "1260, Hib Reinforced Leather Boots", "1760, Stygia Studded Boot", "1761, Stygia Studded Boot", "1801, Aerus Studded Boot",
                "1802, Aerus Studded Boot", "1851, Oceanus Studded Boot", "1852, Oceanus Studded Boot", "2489, TOA Aerus enyalio boot3 a Plate Studded",
                "2711, Possessed Midgard studded boots", "2745, Possessed Albion studded boots", "2776, Possessed Hibernia studded boots",
                "2807, Good Albion studded boots", "2838, Good Hibernia studded boots", "2869, Good Midgard studded boots",
                "3015, Good Shar studded boots", "3094, Possessed Shar reinforced boots", "3114, Possessed Inconnu studded boots",
                "3119, Good Inconnu studded boots", "3134, Poss Inconnu Hib studded boots", "3154, Poss Inconnu Mid studded boots",
                "3175, Possessed Shar Alb studded boots", "3201, Possessed Shar Mid studded boots", "3603, Corrupt studded Boots",
                "3629, Mino studded Boots", "3781, Dragonsworn studded Boots", "4013, Alb Dragonslayer studded Boots",
                "4044, Mid Dragonslayer studded Boots", "4097, Hib Dragonslayer studded Boots",
                // Head
                "1, Full Helmet Studded", "125, Studded Neck Armor", "492, Full Helmet Studded", "824, Albion Studded Helm",
                "827, Hibernia Studded Cap", "829, Norse Studded Helm 1", "830, Norse Studded Helm 2", "831, Norse Studded Helm 3",
                "835, Celtic Reinforced Helm 1", "836, Celtic Reinforced Helm 2", "837, Celtic Reinforced Helm 3", "1199, Celtic Reinforced Helm 4",
                "1215, Norse Studded Helm 4", "1233, Albion Studded Helm 1", "1234, Albion Studded Helm 3", "1235, Albion Studded Helm 5",
                "2260, TOA Oceanus Studded Helm", "2261, TOA Oceanus Studded Helm", "2262, TOA Oceanus Studded Helm", "2278, TOA Oceanus Studded Helm",
                "2279, TOA Oceanus Studded Helm", "2280, TOA Oceanus Studded Helm", "2296, TOA Oceanus Studded Helm", "2297, TOA Oceanus Studded Helm",
                "2298, TOA Oceanus Studded Helm", "2314, TOA Stygia Studded Helm", "2315, TOA Stygia Studded Helm", "2316, TOA Stygia Studded Helm",
                "2332, TOA Stygia Studded Helm", "2333, TOA Stygia Studded Helm", "2334, TOA Stygia Studded Helm", "2350, TOA Stygia Studded Helm",
                "2351, TOA Stygia Studded Helm", "2352, TOA Stygia Studded Helm", "2368, TOA Volcanus Studded Helm", "2369, TOA Volcanus Studded Helm",
                "2370, TOA Volcanus Studded Helm", "2386, TOA Volcanus Studded Helm", "2387, TOA Volcanus Studded Helm",
                "2388, TOA Volcanus Studded Helm", "2404, TOA Volcanus Studded Helm", "2405, TOA Volcanus Studded Helm",
                "2406, TOA Volcanus Studded Helm", "2422, TOA Aerus Studded Helm", "2423, TOA Aerus Studded Helm", "2424, TOA Aerus Studded Helm",
                "2440, TOA Aerus Studded Helm", "2441, TOA Aerus Studded Helm", "2442, TOA Aerus Studded Helm", "2458, TOA Aerus Studded Helm",
                "2459, TOA Aerus Studded Helm", "2460, TOA Aerus Studded Helm", "2580, TOA Oceanus Studded Helm",
                "4055, Albion Dragonslayer Stud Leather Full Helm", "4062, Hib Dragonslayer Stud Leather Full Helm",
                "4069, Mid Dragonslayer Stud Leather Full Helm"
            });

            // Chain
            RegisterArmorBatch(eObjectType.Chain, new[] {
                "41, Chain Hauberk", "181, Chain 2 Hauberk", "186, Chain 3 Hauberk", "191, Chain 4 Hauberk", "196, Special Chain Hauberk",
                "235, Norse Chain Vest", "255, Norse Chain 2 Vest", "275, Norse Chain 3 Vest", "295, Norse Chain Special Vest",
                "999, Norse Chain 3 Tunic", "1246, Albion Chain 3 Hauberk", "1251, Albion Chain 4 Hauberk", "1262, Norse Chain 4 Tunic",
                "1694, Volcanus Chain Body", "1695, Volcanus Chain Body", "1696, Volcanus Chain Body", "1809, Stygia Chain Body",
                "1810, Stygia Chain Body", "1811, Stygia Chain Body", "2101, Oceanus Chain Body", "2102, Oceanus Chain Body",
                "2103, Oceanus Chain Body", "2227, ToA Oceanus Eirene Chestplate", "2228, ToA Oceanus Eirene Chestplate",
                "2476, ToA Aerus Gift Tunic", "2477, ToA Aerus Gift Tunic", "2478, ToA Aerus Gift Tunic", "2511, ToA Oceanus Eirene Chestplate",
                "2608, Briton Chain Test", "2713, Possessed Midgard chain chest", "2747, Possessed Albion chain chest",
                "2778, Possessed Hibernia chain chest", "2809, Good Albion chain chest", "2840, Good Hibernia chain chest",
                "2871, Good Midgard chain chest", "2938, Rediscovered Chain Hauberk", "2994, Good Shar Chain chest", "3028, Possesed Inconnu chain chest",
                "3043, Good Inconnu chain chest", "3106, Possessed Shar chain chest", "3156, Poss Inconnu Mid chain chest",
                "3177, Possessed Shar Alb chain chest", "3203, Possessed Shar Mid chain chest", "3585, Corrupt Chain Vest",
                "3611, Mino Chain Vest", "3763, Dragonsworn Chain Vest", "3995, Alb Dragonslayer Chain Vest", "4026, Mid Dragonslayer Chain Vest",
                "4079, Hib Dragonslayer Chain Vest",
                // Legs
                "42, Chain Leggings", "182, Chain 2 Leggings", "187, Chain 3 Leggings", "192, Chain 4 Leggings", "197, Special Chain Leggings",
                "236, Norse Chain Legs", "256, Norse Chain 2 Legs", "276, Norse Chain 3 Legs", "296, Norse Chain Special Legs",
                "998, Norse Chain 3 Legs", "1247, Albion Chain 3 Leggings", "1252, Albion Chain 4 Leggings", "1261, Norse Chain 4 Legs",
                "1700, Volcanus Chain Legs", "1701, Volcanus Chain Legs", "1815, Stygia Chain Legs", "1816, Stygia Chain Legs",
                "2107, Oceanus Chain Legs", "2108, Oceanus Chain Legs", "2714, Possessed Midgard chain leggings",
                "2748, Possessed Albion chain leggings", "2779, Possessed Hibernia chain leggings", "2810, Good Albion chain leggings",
                "2841, Good Hibernia chain leggings", "2872, Good Midgard chain leggings", "2939, Rediscovered Chain Leggings",
                "2943, Catacomb Norse Guard Chain 3 Legs", "2995, Good Shar Chain pants", "3029, Possesed Inconnu chain leggings",
                "3044, Good Inconnu chain leggings", "3107, Possessed Shar chain leggings", "3157, Poss Inconnu Mid chain leggings",
                "3178, Possessed Shar Alb chain leggings", "3204, Possessed Shar Mid chain leggings", "3586, Corrupt Chain Legs",
                "3612, Mino Chain Legs", "3764, Dragonsworn Chain Legs", "3996, Alb Dragonslayer Chain Legs", "4027, Mid Dragonslayer Chain Legs",
                "4080, Hib Dragonslayer Chain Legs",
                // Arms
                "43, Chain Arms", "126, Chain Shoulder Armor", "127, Chain Bracers", "183, Chain 2 Arms", "188, Chain 3 Arms", "193, Chain 4 Arms",
                "198, Special Chain Arms", "237, Norse Chain Arms", "257, Norse Chain 2 Arms", "277, Norse Chain 3 Arms",
                "297, Norse Chain Special Arms", "1002, Norse Chain 3 Arms", "1248, Albion Chain 3 Arms", "1253, Albion Chain 4 Arms",
                "1265, Norse Chain 4 Arms", "1693, Volcanus Chain Arms", "1808, Stygia Chain Arms", "2100, Oceanus Chain Arms",
                "2715, Possessed Midgard chain sleeves", "2749, Possessed Albion chain sleeves", "2780, Possessed Hibernia chain sleeves",
                "2811, Good Albion chain sleeves", "2842, Good Hibernia chain sleeves", "2873, Good Midgard chain sleeves",
                "2940, Rediscovered Chain Arms", "2944, Catacomb Norse Guard Chain 3 Arms", "2996, Good Shar Chain sleeves",
                "3030, Possesed Inconnu chain sleeves", "3045, Good Inconnu chain sleeves", "3108, Possessed Shar chain sleeves",
                "3158, Poss Inconnu Mid chain sleeves", "3179, Possessed Shar Alb chain sleeves", "3205, Possessed Shar Mid chain sleeves",
                "3587, Corrupt Chain Arms", "3613, Mino Chain Arms", "3765, Dragonsworn Chain Arms", "3997, Alb Dragonslayer Chain Arms",
                "4028, Mid Dragonslayer Chain Arms", "4081, Hib Dragonslayer Chain Arms",
                // Hands
                "44, Chain Gloves", "184, Chain 2 Gloves", "189, Chain 3 Gloves", "194, Chain 4 Gloves", "199, Special Chain Gloves",
                "238, Norse Chain Gloves", "258, Norse Chain 2 Gloves", "278, Norse Chain 3 Gloves", "298, Norse Chain Special Gloves",
                "1000, Norse Chain 3 Gloves", "1249, Albion Chain 3 Gloves", "1254, Albion Chain 4 Gloves", "1263, Norse Chain 4 Gloves",
                "1699, Volcanus Chain Glove", "1814, Stygia Chain Glove", "2106, Oceanus Chain Glove", "2495, Scalar Gloves Chain",
                "2718, Possessed Midgard chain gauntlets", "2752, Possessed Albion chain gauntlets", "2783, Possessed Hibernia chain gauntlets",
                "2814, Good Albion chain gauntlets", "2845, Good Hibernia chain gauntlets", "2876, Good Midgard chain gauntlets",
                "2941, Rediscovered Chain Gloves", "2945, Catacomb Norse Guard Chain 3 Gloves", "2999, Good Shar Chain gauntlets",
                "3032, Possesed Inconnu chain gauntlets", "3047, Good Inconnu chain gauntlets", "3110, Possessed Shar chain gauntlets",
                "3160, Poss Inconnu Mid chain gauntlets", "3181, Possessed Shar Alb chain gauntlets", "3207, Possessed Shar Mid chain gauntlets",
                "3588, Corrupt Chain Gloves", "3614, Mino Chain Gloves", "3766, Dragonsworn Chain Gloves", "3998, Alb Dragonslayer Chain Gloves",
                "4029, Mid Dragonslayer Chain Gloves", "4082, Hib Dragonslayer Chain Gloves",
                // Feet
                "45, Chain Boots", "185, Chain 2 Boots", "190, Chain 3 Boots", "195, Chain 4 Boots", "200, Special Chain Boots",
                "239, Norse Chain Boots", "259, Norse Chain 2 Boots", "279, Norse Chain 3 Boots", "299, Norse Chain Special Boots",
                "1001, Norse Chain 3 Boots", "1250, Albion Chain 3 Boots", "1255, Albion Chain 4 Boots", "1264, Norse Chain 4 Boots",
                "1697, Volcanus Chain Boots", "1698, Volcanus Chain Boots", "1812, Stygia Chain Boots", "1813, Stygia Chain Boots",
                "2104, Oceanus Chain Boots", "2105, Oceanus Chain Boots", "2717, Possessed Midgard chain boots", "2751, Possessed Albion chain boots",
                "2782, Possessed Hibernia chain boots", "2813, Good Albion chain boots", "2844, Good Hibernia chain boots",
                "2875, Good Midgard chain boots", "2942, Rediscovered Chain Boots", "2946, Catacomb Norse Guard Chain 3 Boots",
                "2998, Good Shar Chain boots", "3031, Possesed Inconnu chain boots", "3046, Good Inconnu chain boots",
                "3109, Possessed Shar chain boots", "3159, Poss Inconnu Mid chain boots", "3180, Possessed Shar Alb chain boots",
                "3206, Possessed Shar Mid chain boots", "3589, Corrupt Chain Boots", "3615, Mino Chain Boots", "3767, Dragonsworn Chain Boots",
                "3999, Alb Dragonslayer Chain Boots", "4030, Mid Dragonslayer Chain Boots", "4083, Hib Dragonslayer Chain Boots",
                // Head
                "1, Full Helmet Chain", "63, Albion Chain Helmet 1", "128, Chain Neck Armor", "493, Full Helmet Chain", "832, Norse Chain Helm 1",
                "833, Norse Chain Helm 2", "834, Norse Chain Helm 3", "1216, Norse Chain Helm 4", "1217, Norse Chain/Plate 1",
                "1218, Norse Chain/Plate 2", "1219, Norse Chain/Plate 3", "1220, Norse Chain/Plate 4", "1221, Norse Guard Chain Helm 1",
                "1222, Norse Guard Chain Helm 2", "1223, Norse Guard Chain Helm 3", "1224, Norse Guard Chain Helm 4",
                "1225, Norse Guard Chain/Plate Helm 1", "1226, Norse Guard Chain/Plate Helm 2", "1227, Norse Guard Chain/Plate Helm 3",
                "1228, Norse Guard Chain/Plate Helm 4", "1236, Albion Chain Helm 3", "1237, Albion Chain Helm 5", "1241, Albion Guard Chain Helm 1",
                "1242, Albion Guard Chain Helm 3", "1243, Albion Guard Chain Helm 5", "2257, TOA Oceanus Chain Helm", "2258, TOA Oceanus Chain Helm",
                "2259, TOA Oceanus Chain Helm", "2275, TOA Oceanus Chain Helm", "2276, TOA Oceanus Chain Helm", "2277, TOA Oceanus Chain Helm",
                "2293, TOA Oceanus Chain Helm", "2294, TOA Oceanus Chain Helm", "2295, TOA Oceanus Chain Helm", "2311, TOA Stygia Chain Helm",
                "2312, TOA Stygia Chain Helm", "2313, TOA Stygia Chain Helm", "2329, TOA Stygia Chain Helm", "2330, TOA Stygia Chain Helm",
                "2331, TOA Stygia Chain Helm", "2347, TOA Stygia Chain Helm", "2348, TOA Stygia Chain Helm", "2349, TOA Stygia Chain Helm",
                "2365, TOA Volcanus Chain Helm", "2366, TOA Volcanus Chain Helm", "2367, TOA Volcanus Chain Helm", "2383, TOA Volcanus Chain Helm",
                "2384, TOA Volcanus Chain Helm", "2385, TOA Volcanus Chain Helm", "2401, TOA Volcanus Chain Helm", "2402, TOA Volcanus Chain Helm",
                "2403, TOA Volcanus Chain Helm", "2419, TOA Aerus Chain Helm", "2420, TOA Aerus Chain Helm", "2421, TOA Aerus Chain Helm",
                "2437, TOA Aerus Chain Helm", "2438, TOA Aerus Chain Helm", "2439, TOA Aerus Chain Helm", "2455, TOA Aerus Chain Helm",
                "2456, TOA Aerus Chain Helm", "2457, TOA Aerus Chain Helm", "4057, Albion Dragonslayer Alb Chain Full Helm",
                "4058, Albion Dragonslayer Mid Chain Full Helm", "4064, Hib Dragonslayer Albion Chain Full Helm",
                "4065, Hib Dragonslayer Midgard Chain Full Helm", "4071, Mid Dragonslayer Alb Chain Full Helm",
                "4072, Mid Dragonslayer Mid Chain Full Helm"
            });

            // Plate
            RegisterArmorBatch(eObjectType.Plate, new[] {
                "46, Alb Plate 1 Breast", "86, Alb Plate 2 Breast", "201, Plate 3 Breast", "206, Plate 4 Breast", "211, Special Plate Breast",
                "662, Briton Guard Breastplate", "667, Hibernian Guard Breastplate", "668, Norse Guard Breastplate", "688, Albion Armsman 50 Breast",
                "693, Albion Paladin 50 Breast", "1272, Alb Plate 3 Scale Breast", "1685, Aerus Plate Body", "1686, Aerus Plate Body",
                "1687, Aerus Plate Body", "1703, Volcanus Plate Body", "1704, Volcanus Plate Body", "1705, Volcanus Plate Body",
                "2092, Oceanus Plate Body", "2093, Oceanus Plate Body", "2094, Oceanus Plate Body", "2124, Stygia Plate Body",
                "2125, Stygia Plate Body", "2126, Stygia Plate Body", "2226, ToA Oceanus Eirene Chestplate", "2479, ToA Aerus Gift Tunic",
                "2719, Possessed Midgard plate chest", "2753, Possessed Albion plate chest", "2784, Possessed Hibernia plate chest",
                "2815, Good Albion plate chest", "2846, Good Hibernia plate chest", "2877, Good Midgard plate chest", "3006, Good Shar plate chest",
                "3038, Possesed Inconnu plate chest", "3053, Good Inconnu plate chest", "3101, Possessed Shar plate chest",
                "3182, Possessed Shar Alb plate chest", "3590, Corrupt Plate Chest", "3616, Mino Plate Chest", "3768, Dragonsworn Plate Chest",
                "4000, Alb Dragonslayer Plate Chest", "4031, Mid Dragonslayer Plate Chest", "4084, Hib Dragonslayer Plate Chest",
                // Legs
                "47, Alb Plate 1 Legs", "87, Alb Plate 2 Legs", "202, Plate 3 Legs", "207, Plate 4 Legs", "212, Special Plate Legs",
                "663, Briton Guard Legs", "689, Albion Armsman 50 Legs", "694, Albion Paladin 50 Legs", "1273, Alb Plate 3 Scale Legs",
                "1691, Aerus Plate Legs", "1692, Aerus Plate Legs", "1709, Volcanus Plate Legs", "1710, Volcanus Plate Legs",
                "2098, Oceanus Plate Legs", "2099, Oceanus Plate Legs", "2130, Stygia Plate Legs", "2131, Stygia Plate Legs",
                "2510, Alvarus Legs", "2720, Possessed Midgard plate leggings", "2754, Possessed Albion plate leggings",
                "2785, Possessed Hibernia plate leggings", "2816, Good Albion plate leggings", "2847, Good Hibernia plate leggings",
                "2878, Good Midgard plate leggings", "3007, Good Shar plate leggings", "3039, Possesed Inconnu plate leggings",
                "3054, Good Inconnu plate leggings", "3102, Possessed Shar plate leggings", "3183, Possessed Shar Alb plate leggings",
                "3591, Corrupt Plate Legs", "3617, Mino Plate Legs", "3769, Dragonsworn Plate Legs", "4001, Alb Dragonslayer Plate Legs",
                "4032, Mid Dragonslayer Plate Legs", "4085, Hib Dragonslayer Plate Legs",
                // Arms
                "48, Alb Plate 1 Arms", "88, Alb Plate 2 Arms", "129, Plate Shoulder Armor", "130, Plate Bracers", "203, Plate 3 Arms",
                "208, Plate 4 Arms", "213, Special Plate Arms", "664, Briton Guard Arms", "690, Albion Armsman 50 Arms",
                "695, Albion Paladin 50 Arms", "1274, Alb Plate 3 Scale Arms", "1684, Aerus Plate Arms", "1702, Volcanus Plate Arms",
                "2091, Oceanus Plate Arms", "2123, Stygia Plate Arms", "2503, TOA Oceanus winds arm Plate", "2721, Possessed Midgard plate sleeves",
                "2755, Possessed Albion plate sleeves", "2786, Possessed Hibernia plate sleeves", "2817, Good Albion plate sleeves",
                "2848, Good Hibernia plate sleeves", "2879, Good Midgard plate sleeves", "3008, Good Shar plate sleeves",
                "3040, Possesed Inconnu plate sleeves", "3055, Good Inconnu plate sleeves", "3103, Possessed Shar plate sleeves",
                "3184, Possessed Shar Alb plate sleeves", "3592, Corrupt Plate Arms", "3618, Mino Plate Arms", "3770, Dragonsworn Plate Arms",
                "4002, Alb Dragonslayer Plate Arms", "4033, Mid Dragonslayer Plate Arms", "4086, Hib Dragonslayer Plate Arms",
                // Hands
                "49, Alb Plate 1 Gloves", "89, Alb Plate 2 Gloves", "204, Plate 3 Gloves", "209, Plate 4 Gloves", "214, Special Plate Gloves",
                "665, Briton Guard Gloves", "691, Albion Armsman 50 Gloves", "696, Albion Paladin 50 Gloves", "1275, Alb Plate 3 Scale Gloves",
                "1690, Aerus Plate Glove", "1708, Volcanus Plate Glove", "2097, Oceanus Plate Glove", "2129, Stygia Plate Glove",
                "2724, Possessed Midgard plate gauntlets", "2758, Possessed Albion plate gauntlets", "2789, Possessed Hibernia plate gauntlets",
                "2820, Good Albion plate gauntlets", "2851, Good Hibernia plate gauntlets", "2882, Good Midgard plate gauntlets",
                "3011, Good Shar plate gauntlets", "3042, Possessed Inconnu plate gauntlets", "3057, Good Inconnu plate gauntlets",
                "3105, Possessed Shar plate gauntlets", "3186, Possessed Shar Alb plate gauntlets", "3593, Corrupt Plate Gloves",
                "3619, Mino Plate Gloves", "3771, Dragonsworn Plate Gloves", "4003, Alb Dragonslayer Plate Gloves",
                "4034, Mid Dragonslayer Plate Gloves", "4087, Hib Dragonslayer Plate Gloves",
                // Feet
                "50, Alb Plate 1 Boots", "90, Alb Plate 2 Boots", "205, Plate 3 Boots", "210, Plate 4 Boots", "215, Special Plate Boots",
                "666, Briton Guard Boots", "692, Albion Armsman 50 Boots", "697, Albion Paladin 50 Boots", "1276, Alb Plate 3 Scale Boots",
                "1688, Aerus Plate Boots", "1689, Aerus Plate Boots", "1706, Volcanus Plate Boots", "1707, Volcanus Plate Boots",
                "2095, Oceanus Plate Boots", "2096, Oceanus Plate Boots", "2127, Stygia Plate Boots", "2128, Stygia Plate Boots",
                "2487, TOA Aerus enyalio boot3 a Plate", "2723, Possessed Midgard plate boots", "2757, Possessed Albion plate boots",
                "2788, Possessed Hibernia plate boots", "2819, Good Albion plate boots", "2850, Good Hibernia plate boots",
                "2881, Good Midgard plate boots", "3010, Good Shar plate boots", "3041, Possesed Inconnu plate boots", "3056, Good Inconnu plate boots",
                "3104, Possessed Shar plate boots", "3185, Possessed Shar Alb plate boots", "3594, Corrupt Plate Boots", "3620, Mino Plate Boots",
                "3772, Dragonsworn Plate Boots", "4004, Alb Dragonslayer Plate Boots", "4035, Mid Dragonslayer Plate Boots",
                "4088, Hib Dragonslayer Plate Boots",
                // Head
                "64, Albion Plate Helmet 1", "93, Albion Plate Helmet 2", "94, Albion Guard Plate Helmet 1", "95, Albion Guard Plate Helmet 2",
                "131, Plate Neck Armor", "1238, Albion Plate Helm 3", "1239, Albion Plate Helm 4", "1240, Albion Plate Helm 5",
                "1244, Albion Guard Plate Helm 3", "1245, Albion Guard Plate Helm 5", "2266, TOA Oceanus Plate Helm", "2267, TOA Oceanus Plate Helm",
                "2268, TOA Oceanus Plate Helm", "2284, TOA Oceanus Plate Helm", "2285, TOA Oceanus Plate Helm", "2286, TOA Oceanus Plate Helm",
                "2302, TOA Oceanus Plate Helm", "2303, TOA Oceanus Plate Helm", "2304, TOA Oceanus Plate Helm", "2320, TOA Stygia Plate Helm",
                "2321, TOA Stygia Plate Helm", "2322, TOA Stygia Plate Helm", "2338, TOA Stygia Plate Helm", "2339, TOA Stygia Plate Helm",
                "2340, TOA Stygia Plate Helm", "2356, TOA Stygia Plate Helm", "2357, TOA Stygia Plate Helm", "2358, TOA Stygia Plate Helm",
                "2374, TOA Volcanus Plate Helm", "2375, TOA Volcanus Plate Helm", "2376, TOA Volcanus Plate Helm", "2392, TOA Volcanus Plate Helm",
                "2393, TOA Volcanus Plate Helm", "2394, TOA Volcanus Plate Helm", "2410, TOA Volcanus Plate Helm", "2411, TOA Volcanus Plate Helm",
                "2412, TOA Volcanus Plate Helm", "2428, TOA Aerus Plate Helm", "2429, TOA Aerus Plate Helm", "2430, TOA Aerus Plate Helm",
                "2446, TOA Aerus Plate Helm", "2447, TOA Aerus Plate Helm", "2448, TOA Aerus Plate Helm", "2464, TOA Aerus Plate Helm",
                "2465, TOA Aerus Plate Helm", "2466, TOA Aerus Plate Helm", "4053, Albion Dragonslayer Plate Full Helm",
                "4060, Hib Dragonslayer Plate Full Helm", "4067, Mid Dragonslayer Plate Full Helm"
            });

            // Scale
            RegisterArmorBatch(eObjectType.Scale, new[] {
                "348, Celtic Scaled Special Vest", "368, Celtic Scaled Worn Vest", "388, Celtic Scaled 1 Vest", "408, Celtic Scaled 2 Vest",
                "428, Celtic Scaled 3 Vest", "708, Hibernian Hero Scaled Vest", "988, Isles Hib Scaled 1 Vest", "1712, Volcanus Scale Body",
                "1713, Volcanus Scale Body", "1714, Volcanus Scale Body", "1748, Aerus Scale Body", "1749, Aerus Scale Body",
                "1750, Aerus Scale Body", "1771, Oceanus Scale Body", "1772, Oceanus Scale Body", "1773, Oceanus Scale Body",
                "2480, ToA Aerus Gift Tunic", "2713, Possessed Midgard scale chest", "2747, Possessed Albion scale chest",
                "2778, Possessed Hibernia scale chest", "2809, Good Albion scale chest", "2840, Good Hibernia scale chest",
                "2871, Good Midgard scale chest", "3000, Good Shar Scale chest", "3033, Possesed Inconnu scale chest", "3048, Good Inconnu scale chest",
                "3136, Poss Inconnu Hib scale chest", "3595, Corrupt Scale Vest", "3621, Mino Scale Vest", "3773, Dragonsworn Scale Vest",
                "4005, Alb Dragonslayer Scale Vest", "4036, Mid Dragonslayer Scale Vest", "4089, Hib Dragonslayer Scale Vest",
                // Legs
                "349, Celtic Scaled Special Legs", "369, Celtic Scaled Worn Legs", "389, Celtic Scaled 1 Legs", "409, Celtic Scaled 2 Legs",
                "429, Celtic Scaled 3 Legs", "709, Hibernian Hero Scaled Legs", "989, Isles Hib Scaled 1 Legs", "1718, Volcanus Scale Legs",
                "1719, Volcanus Scale Legs", "1754, Aerus Scale Legs", "1755, Aerus Scale Legs", "1777, Oceanus Scale Legs", "1778, Oceanus Scale Legs",
                "2714, Possessed Midgard scale leggings", "2748, Possessed Albion scale leggings", "2779, Possessed Hibernia scale leggings",
                "2810, Good Albion scale leggings", "2841, Good Hibernia scale leggings", "2872, Good Midgard scale leggings",
                "3001, Good Shar Scale leggings", "3034, Possesed Inconnu scale leggings", "3049, Good Inconnu scale leggings",
                "3137, Poss Inconnu Hib scale leggings", "3596, Corrupt Scale Legs", "3622, Mino Scale Legs", "3774, Dragonsworn Scale Legs",
                "4006, Alb Dragonslayer Scale Legs", "4037, Mid Dragonslayer Scale Legs", "4090, Hib Dragonslayer Scale Legs",
                // Arms
                "350, Celtic Scaled Special Arms", "370, Celtic Scaled Worn Arms", "390, Celtic Scaled 1 Arms", "410, Celtic Scaled 2 Arms",
                "430, Celtic Scaled 3 Arms", "710, Hibernian Hero Scaled Arms", "990, Isles Hib Scaled 1 Arms", "1711, Volcanus Scale Arms",
                "1747, Aerus Scale Arms", "1770, Oceanus Scale Arms", "2715, Possessed Midgard scale sleeves", "2749, Possessed Albion scale sleeves",
                "2780, Possessed Hibernia scale sleeves", "2811, Good Albion scale sleeves", "2842, Good Hibernia scale sleeves",
                "2873, Good Midgard scale sleeves", "3002, Good Shar Scale sleeves", "3035, Possesed Inconnu scale sleeves",
                "3050, Good Inconnu scale sleeves", "3138, Poss Inconnu Hib scale sleeves", "3597, Corrupt Scale Arms", "3623, Mino Scale Arms",
                "3775, Dragonsworn Scale Arms", "4007, Alb Dragonslayer Scale Arms", "4038, Mid Dragonslayer Scale Arms",
                "4091, Hib Dragonslayer Scale Arms",
                // Hands
                "351, Celtic Scaled Special Gloves", "371, Celtic Scaled Worn Gloves", "391, Celtic Scaled 1 Gloves", "411, Celtic Scaled 2 Gloves",
                "431, Celtic Scaled 3 Gloves", "711, Hibernian Hero Scaled Gloves", "991, Isles Hib Scaled 1 Gloves", "1717, Volcanus Scale Glove",
                "1746, Scalar Gloves", "1753, Aerus Scale Glove", "1776, Oceanus Scale Glove", "2718, Possessed Midgard scale gauntlets",
                "2752, Possessed Albion scale gauntlets", "2783, Possessed Hibernia scale gauntlets", "2814, Good Albion scale gauntlets",
                "2845, Good Hibernia scale gauntlets", "2876, Good Midgard scale gauntlets", "3005, Good Shar Scale gauntlets",
                "3037, Possesed Inconnu scale gauntlets", "3052, Good Inconnu scale gauntlets", "3140, Poss Inconnu Hib scale gauntlets",
                "3598, Corrupt Scale Gloves", "3624, Mino Scale Gloves", "3776, Dragonsworn Scale Gloves", "4008, Alb Dragonslayer Scale Gloves",
                "4039, Mid Dragonslayer Scale Gloves", "4092, Hib Dragonslayer Scale Gloves",
                // Feet
                "352, Celtic Scaled Special Boots", "372, Celtic Scaled Worn Boots", "392, Celtic Scaled 1 Boots", "412, Celtic Scaled 2 Boots",
                "432, Celtic Scaled 3 Boots", "712, Hibernian Hero Scaled Boots", "992, Isles Hib Scaled 1 Boots", "1715, Volcanus Scale Boots",
                "1716, Volcanus Scale Boots", "1751, Aerus Scale Boots", "1752, Aerus Scale Boots", "1774, Oceanus Scale Boots",
                "1775, Oceanus Scale Boots", "2717, Possessed Midgard scale boots", "2751, Possessed Albion scale boots",
                "2782, Possessed Hibernia scale boots", "2813, Good Albion scale boots", "2844, Good Hibernia scale boots",
                "2875, Good Midgard scale boots", "3004, Good Shar Scale boots", "3036, Possesed Inconnu scale boots", "3051, Good Inconnu scale boots",
                "3139, Poss Inconnu Hib scale boots", "3599, Corrupt Scale Boots", "3625, Mino Scale Boots", "3777, Dragonsworn Scale Boots",
                "4009, Alb Dragonslayer Scale Boots", "4040, Mid Dragonslayer Scale Boots", "4093, Hib Dragonslayer Scale Boots",
                // Head
                "838, Celtic Scale Helm 1", "839, Celtic Scale Helm 2", "840, Celtic Scale Helm 3", "1200, Celtic Scale Helm 4",
                "1201, Celtic Scale/Plate Helm 1", "1202, Celtic Scale/Plate Helm 2", "1203, Celtic Scale/Plate Helm 3",
                "1204, Celtic Scale/Plate Helm 4", "1205, Celtic Guard Scale Helm 1", "1206, Celtic Guard Scale Helm 2",
                "1207, Celtic Guard Scale Helm 3", "1208, Celtic Guard Scale Helm 4", "1209, Celtic Guard Scale/Plate Helm 1",
                "1210, Celtic Guard Scale/Plate Helm 2", "1211, Celtic Guard Scale/Plate Helm 3", "1212, Celtic Guard Scale/Plate Helm 4",
                "2263, TOA Oceanus Scale Helm", "2264, TOA Oceanus Scale Helm", "2265, TOA Oceanus Scale Helm", "2281, TOA Oceanus Scale Helm",
                "2282, TOA Oceanus Scale Helm", "2283, TOA Oceanus Scale Helm", "2299, TOA Oceanus Scale Helm", "2300, TOA Oceanus Scale Helm",
                "2301, TOA Oceanus Scale Helm", "2317, TOA Stygia Scale Helm", "2318, TOA Stygia Scale Helm", "2319, TOA Stygia Scale Helm",
                "2335, TOA Stygia Scale Helm", "2336, TOA Stygia Scale Helm", "2337, TOA Stygia Scale Helm", "2353, TOA Stygia Scale Helm",
                "2354, TOA Stygia Scale Helm", "2355, TOA Stygia Scale Helm", "2371, TOA Volcanus Scale Helm", "2372, TOA Volcanus Scale Helm",
                "2373, TOA Volcanus Scale Helm", "2389, TOA Volcanus Scale Helm", "2390, TOA Volcanus Scale Helm", "2391, TOA Volcanus Scale Helm",
                "2407, TOA Volcanus Scale Helm", "2408, TOA Volcanus Scale Helm", "2409, TOA Volcanus Scale Helm", "2425, TOA Aerus Scale Helm",
                "2426, TOA Aerus Scale Helm", "2427, TOA Aerus Scale Helm", "2443, TOA Aerus Scale Helm", "2444, TOA Aerus Scale Helm",
                "2445, TOA Aerus Scale Helm", "2461, TOA Aerus Scale Helm", "2462, TOA Aerus Scale Helm", "2463, TOA Aerus Scale Helm",
                "4059, Albion Dragonslayer Scale Full Helm", "4066, Hib Dragonslayer Scale Full Helm", "4073, Mid Dragonslayer Scale Full Helm"
            });

            // ==========================================================
            // MAGICAL CLOAK
            // ==========================================================
            RegisterBatch(eObjectType.Magical, eInventorySlot.Cloak, new[] {
                "57, Cloak", "91, Guard Cloak Briton", "92, Guard Cloak Avalonian", "96, Hooded Cloak", "144, Guardian Cloak", "326, Norse Cloak",
                "443, Mage Cloak", "467, Celtic Cloak", "557, Mage Cloak 2", "558, Mage Cloak 3", "559, Cloak 2", "560, Cloak 3", "669, Collar Cloak",
                "676, PVP Guard Cloak Briton", "677, PvP Guard Cloak Midgard", "678, PvPGuard Cloak Hibernia", "1720, Feather Cloak",
                "1721, Harpy Cloak", "1722, Scaled Cloak", "1723, Healing Cloak", "1724, Stygian Cloak",
                "1725, Magma Cloak", "1726, Mist Cloak", "1727, Cloud Cloak", "3632, Corrupt Mino Cloak 1",
                "3633, Corrupt Mino Cloak 2", "3634, Corrupt Mino Cloak 3", "3635, Regular Mino Cloak 1", "3636, Regular Mino Cloak 2",
                "3637, Regular Mino Cloak 3", "3752, Valentines Day Cloak", "3789, Dragonsworn Cloak Emblem", "3790, Dragonsworn Cloak Ornate",
                "3791, Dragonsworn Cloak Standard", "3800, Albion Realm Cloak", "3801, Midgard Realm Cloak", "3802, Hibernian Realm Cloak",
                "4105, Albion Dragonslayer Ornate Cloak", "4106, Albion Dragonlslayer Simple Cloak", "4107, Midgard Dragonslayer Ornate Cloak",
                "4108, Midgard Dragonlslayer Simple Cloak", "4109, Hibernia Dragonslayer Ornate Cloak", "4110, Hibernia Dragonlslayer Simple Cloak",
                "4115, White Star Winter Cloak", "4464, VFX Test Cloak", "4557, Albion Otherworldly Cloak", "4558, Midgard Otherworldly Cloak",
                "4559, Hibernian Otherworldly Cloak", "4604, cloak_wizard_Epic01", "4605, cloak_animist_Epic01", "4606, cloak_armsman_Epic01",
                "4607, cloak_bainshee_Epic01", "4608, cloak_bard_Epic01", "4609, cloak_berserker_Epic01", "4610, cloak_blademaster_Epic01",
                "4611, cloak_bonedancer_Epic01", "4612, cloak_cabalist_Epic01", "4613, cloak_champion_Epic01", "4614, cloak_cleric_Epic01",
                "4615, cloak_druid_Epic01", "4616, cloak_eldritch_Epic01", "4617, cloak_enchanter_Epic01", "4618, cloak_friar_Epic01",
                "4619, cloak_healer_Epic01", "4620, cloak_heretic_Epic01", "4621, cloak_hero_Epic01", "4622, cloak_hunter_Epic01",
                "4623, cloak_infiltrator_Epic01", "4624, cloak_mentalist_Epic01", "4625, cloak_mercenary_Epic01", "4626, cloak_minstrel_Epic01",
                "4627, cloak_necromancer_Epic01", "4628, cloak_nightshade_Epic01", "4629, cloak_paladin_Epic01", "4630, cloak_ranger_Epic01",
                "4631, cloak_reaver_Epic01", "4632, cloak_runemaster_Epic01", "4633, cloak_savage_Epic01", "4634, cloak_scout_Epic01",
                "4635, cloak_shadowblade_Epic01", "4636, cloak_shaman_Epic01", "4637, cloak_skald_Epic01", "4638, cloak_sorcerer_Epic01",
                "4639, cloak_spiritmaster_Epic01", "4640, cloak_theurgist_Epic01", "4641, cloak_valewalker_Epic01", "4642, cloak_vampire_Epic01",
                "4643, cloak_warden_Epic01", "4644, cloak_Thane_Epic01", "4645, cloak_Valkyrie_Epic01", "4646, cloak_Warlock_Epic01",
                "4647, cloak_Warrior_Epic01", "4648, cloak_MaulerALB_Epic01", "4649, cloak_MaulerMID_Epic01", "4650, cloak_MaulerHIB_Epic01",
                "4799, Bounty Accessory Mold", "4822, cloak_angel01", "4823, cloak_demon01", "4836, cloak_zelda01"
            });
        }

        #region Helpers

        private static void Register(int model, string name, eObjectType type, eInventorySlot slot, eRealm realm)
        {
            if (!_definitions.ContainsKey(model))
            {
                _definitions.Add(model, new ModelDefinition { BaseName = name, ObjType = type, Slot = slot, RealmOrigin = realm });
            }
        }

        private static void RegisterBatch(eObjectType type, eInventorySlot slot, string[] items)
        {
            foreach (var line in items)
            {
                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int model))
                {
                    string name = parts[1].Trim();
                    eRealm realm = GetRealmFromName(name);
                    Register(model, name, type, slot, realm);
                }
            }
        }

        // Auto-detects slot from name for armors
        private static void RegisterArmorBatch(eObjectType type, string[] items)
        {
            foreach (var line in items)
            {
                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int model))
                {
                    string name = parts[1].Trim();
                    eInventorySlot slot = GetSlotFromName(name);
                    eRealm realm = GetRealmFromName(name);
                    Register(model, name, type, slot, realm);
                }
            }
        }

        // Helper for Swords 1H: Maps Albion->Slash, Mid->Sword, Hib->Blades
        private static void RegisterSwords1H(eInventorySlot slot, string[] items)
        {
            foreach (var line in items)
            {
                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int model))
                {
                    string name = parts[1].Trim();
                    eRealm realm = GetRealmFromName(name);
                    eObjectType type = eObjectType.Sword; // Default Mid/Generic
                    if (realm == eRealm.Albion) type = eObjectType.SlashingWeapon;
                    if (realm == eRealm.Hibernia) type = eObjectType.Blades;
                    Register(model, name, type, slot, realm);
                }
            }
        }

        // Helper for Swords 2H: Hib->LargeWeapons
        private static void RegisterSwords2H(eInventorySlot slot, string[] items)
        {
            foreach (var line in items)
            {
                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int model))
                {
                    string name = parts[1].Trim();
                    eRealm realm = GetRealmFromName(name);
                    eObjectType type = eObjectType.TwoHandedWeapon;
                    if (realm == eRealm.Hibernia) type = eObjectType.LargeWeapons;
                    Register(model, name, type, slot, realm);
                }
            }
        }

        // Helper for Daggers: Alb->Thrust, Mid->Sword, Hib->Piercing
        private static void RegisterDaggers(eInventorySlot slot, string[] items)
        {
            foreach (var line in items)
            {
                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int model))
                {
                    string name = parts[1].Trim();
                    eRealm realm = GetRealmFromName(name);
                    eObjectType type = eObjectType.ThrustWeapon; // Default/Alb
                    if (realm == eRealm.Midgard) type = eObjectType.Sword;
                    if (realm == eRealm.Hibernia) type = eObjectType.Piercing;
                    Register(model, name, type, slot, realm);
                }
            }
        }

        // Helper for Axes 1H: Alb->Slash, Mid->Axe, Hib->Blades
        private static void RegisterAxes1H(eInventorySlot slot, string[] items)
        {
            foreach (var line in items)
            {
                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int model))
                {
                    string name = parts[1].Trim();
                    eRealm realm = GetRealmFromName(name);
                    eObjectType type = eObjectType.Axe; // Default Mid
                    if (realm == eRealm.Albion) type = eObjectType.SlashingWeapon;
                    if (realm == eRealm.Hibernia) type = eObjectType.Blades;
                    Register(model, name, type, slot, realm);
                }
            }
        }

        // Helper for Axes 2H: Hib->LargeWeapons
        private static void RegisterAxes2H(eInventorySlot slot, string[] items)
        {
            foreach (var line in items)
            {
                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int model))
                {
                    string name = parts[1].Trim();
                    eRealm realm = GetRealmFromName(name);
                    eObjectType type = eObjectType.TwoHandedWeapon;
                    if (realm == eRealm.Hibernia) type = eObjectType.LargeWeapons;
                    Register(model, name, type, slot, realm);
                }
            }
        }

        // Helper for Hammers 1H: Alb->Crush, Mid->Hammer, Hib->Blunt
        private static void RegisterHammers1H(eInventorySlot slot, string[] items)
        {
            foreach (var line in items)
            {
                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int model))
                {
                    string name = parts[1].Trim();
                    eRealm realm = GetRealmFromName(name);
                    eObjectType type = eObjectType.CrushingWeapon; // Default/Alb
                    if (realm == eRealm.Midgard) type = eObjectType.Hammer;
                    if (realm == eRealm.Hibernia) type = eObjectType.Blunt;
                    Register(model, name, type, slot, realm);
                }
            }
        }

        // Helper for Hammers 2H: Hib->LargeWeapons
        private static void RegisterHammers2H(eInventorySlot slot, string[] items)
        {
            foreach (var line in items)
            {
                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int model))
                {
                    string name = parts[1].Trim();
                    eRealm realm = GetRealmFromName(name);
                    eObjectType type = eObjectType.TwoHandedWeapon;
                    if (realm == eRealm.Hibernia) type = eObjectType.LargeWeapons;
                    Register(model, name, type, slot, realm);
                }
            }
        }

        // Helper for Polearms: Alb->Polearm, Mid->Spear, Hib->CelticSpear
        private static void RegisterPolearms(eInventorySlot slot, string[] items)
        {
            foreach (var line in items)
            {
                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int model))
                {
                    string name = parts[1].Trim();
                    eRealm realm = GetRealmFromName(name);
                    eObjectType type = eObjectType.PolearmWeapon; // Default/Alb
                    if (realm == eRealm.Midgard) type = eObjectType.Spear;
                    if (realm == eRealm.Hibernia) type = eObjectType.CelticSpear;
                    Register(model, name, type, slot, realm);
                }
            }
        }

        // Helper for Ranged: Alb->Longbow/Crossbow, Mid->Composite, Hib->Recurve
        private static void RegisterRanged(eInventorySlot slot, string[] items)
        {
            foreach (var line in items)
            {
                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int model))
                {
                    string name = parts[1].Trim();
                    eRealm realm = GetRealmFromName(name);
                    eObjectType type = eObjectType.Longbow; // Default
                    if (name.IndexOf("Crossbow", StringComparison.OrdinalIgnoreCase) >= 0) type = eObjectType.Crossbow;
                    else if (realm == eRealm.Midgard) type = eObjectType.CompositeBow;
                    else if (realm == eRealm.Hibernia) type = eObjectType.RecurvedBow;
                    Register(model, name, type, slot, realm);
                }
            }
        }

        public static bool TryGetDefinition(int model, out ModelDefinition def)
        {
            return _definitions.TryGetValue(model, out def);
        }

        private static eRealm GetRealmFromName(string name)
        {
            string n = name.ToLower();
            if (n.Contains("alb") || n.Contains("briton") || n.Contains("avalon") || n.Contains("paladin") || n.Contains("mercenary") || n.Contains("infiltrator") || n.Contains("friar") || n.Contains("saracen"))
                return eRealm.Albion;
            if (n.Contains("mid") || n.Contains("norse") || n.Contains("troll") || n.Contains("kobold") || n.Contains("viking") || n.Contains("dwarven") || n.Contains("skald") || n.Contains("thane"))
                return eRealm.Midgard;
            if (n.Contains("hib") || n.Contains("celtic") || n.Contains("elf") || n.Contains("firbolg") || n.Contains("lurikeen") || n.Contains("leaf") || n.Contains("blademaster"))
                return eRealm.Hibernia;
            return eRealm.None;
        }

        private static eInventorySlot GetSlotFromName(string name)
        {
            string n = name.ToLower();
            if (n.Contains("vest") || n.Contains("tunic") || n.Contains("robe") || n.Contains("hauberk") || n.Contains("breast") || n.Contains("jerkin") || n.Contains("chest"))
                return eInventorySlot.TorsoArmor;
            if (n.Contains("leg") || n.Contains("pant") || n.Contains("breeches"))
                return eInventorySlot.LegsArmor;
            if (n.Contains("arm") || n.Contains("sleeve"))
                return eInventorySlot.ArmsArmor;
            if (n.Contains("hand") || n.Contains("glove") || n.Contains("gauntlet"))
                return eInventorySlot.HandsArmor;
            if (n.Contains("foot") || n.Contains("feet") || n.Contains("boot"))
                return eInventorySlot.FeetArmor;
            if (n.Contains("head") || n.Contains("helm") || n.Contains("cap") || n.Contains("coif") || n.Contains("circlet"))
                return eInventorySlot.HeadArmor;

            return eInventorySlot.TorsoArmor; // Fallback
        }
        #endregion
    }
}