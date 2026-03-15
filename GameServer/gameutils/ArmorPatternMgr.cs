using System;
using System.Collections.Generic;
using DOL.Database;

namespace DOL.GS
{
    public static class ArmorPatternMgr
    {
        public enum PatternType
        {
            None,
            Possessed,
            Good,
            Corrupt,
            Minotaur,
            Oceanus,
            Stygia,
            Volcanus,
            Aerus
        }

        private static readonly Dictionary<string, PatternType> TemplateIdToType = new Dictionary<string, PatternType>
        {
            { "Possessed_Armor_pattern", PatternType.Possessed },
            { "Good_Armor_pattern", PatternType.Good },
            { "Corrupt_Armor_pattern", PatternType.Corrupt },
            { "Minotaur_Armor_pattern", PatternType.Minotaur },
            { "Oceanus_Armor_pattern", PatternType.Oceanus },
            { "Stygia_Armor_pattern", PatternType.Stygia },
            { "Volcanus_Armor_pattern", PatternType.Volcanus },
            { "Aerus_Armor_pattern", PatternType.Aerus },
        };

        public static PatternType GetPatternType(string id_nb)
        {
            return TemplateIdToType.ContainsKey(id_nb) ? TemplateIdToType[id_nb] : PatternType.None;
        }

        public static int GetModel(PatternType type, InventoryItem item)
        {
            int slot = item.Item_Type;       // 25=Torso, 21=Helm, etc.
            int material = item.Object_Type; // 32=Cloth, 33=Leather, 35=Chain, 36=Plate, etc.
            int realm = item.Realm;

            if (realm == 0)
            {
                if (material == 36) realm = 1; // Plate = Alb
                else if (material == 35) realm = 2; // Chain = Mid
                else if (material == 38) realm = 3; // Scale = Hib
                else if (material == 37) realm = 3; // Reinforced = Hib
                else if (material == 34) realm = 2; // Studded = Mid
                else realm = 1;
            }

            switch (type)
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

            return -1;
        }

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
    }
}