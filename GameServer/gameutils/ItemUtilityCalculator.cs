using System;
using DOL.Database;

namespace DOL.GS
{
    public static class ItemUtilityCalculator
    {
        public static double GetTotalUtility(GameInventoryItem item)
        {
            if (item == null) return 0;
            double totalUtility = 0;
            totalUtility += GetSingleUtility(item.Bonus1Type, item.Bonus1);
            totalUtility += GetSingleUtility(item.Bonus2Type, item.Bonus2);
            totalUtility += GetSingleUtility(item.Bonus3Type, item.Bonus3);
            totalUtility += GetSingleUtility(item.Bonus4Type, item.Bonus4);
            totalUtility += GetSingleUtility(item.Bonus5Type, item.Bonus5);
            totalUtility += GetSingleUtility(item.Bonus6Type, item.Bonus6);
            totalUtility += GetSingleUtility(item.Bonus7Type, item.Bonus7);
            totalUtility += GetSingleUtility(item.Bonus8Type, item.Bonus8);
            totalUtility += GetSingleUtility(item.Bonus9Type, item.Bonus9);
            totalUtility += GetSingleUtility(item.Bonus10Type, item.Bonus10);
            totalUtility += GetSingleUtility(item.ExtraBonusType, item.ExtraBonus);
            return totalUtility;
        }

        public static double GetTotalUtility(ItemTemplate item)
        {
            if (item == null) return 0;
            double totalUtility = 0;
            totalUtility += GetSingleUtility(item.Bonus1Type, item.Bonus1);
            totalUtility += GetSingleUtility(item.Bonus2Type, item.Bonus2);
            totalUtility += GetSingleUtility(item.Bonus3Type, item.Bonus3);
            totalUtility += GetSingleUtility(item.Bonus4Type, item.Bonus4);
            totalUtility += GetSingleUtility(item.Bonus5Type, item.Bonus5);
            totalUtility += GetSingleUtility(item.Bonus6Type, item.Bonus6);
            totalUtility += GetSingleUtility(item.Bonus7Type, item.Bonus7);
            totalUtility += GetSingleUtility(item.Bonus8Type, item.Bonus8);
            totalUtility += GetSingleUtility(item.Bonus9Type, item.Bonus9);
            totalUtility += GetSingleUtility(item.Bonus10Type, item.Bonus10);
            totalUtility += GetSingleUtility(item.ExtraBonusType, item.ExtraBonus);
            return totalUtility;
        }

        public static double GetSingleUtility(int bonusType, int bonus)
        {
            if (bonusType == 0 || bonus == 0)
                return 0;

            return bonusType switch
            {
                < 9 or 156 => bonus * 0.6667,

                145 or 148 => bonus * 1.0,

                10 or 59 or 187 or 210 => bonus * 0.25,

                9 or (>= 11 and <= 19) or 71 or 116 or 119 or 146 or 147 or 149 or
                (>= 169 and <= 172) or (>= 175 and <= 186) or 189 or 190 or
                (>= 192 and <= 196) or 199 or (>= 201 and <= 209) or 211 or
                (>= 217 and <= 220) or 230 or 231 or 246 or 251 or 252 or
                (>= 256 and <= 269) => bonus * 2.0,

                117 or (>= 221 and <= 229) or (>= 236 and <= 245) => bonus * 4.0,

                (>= 20 and <= 58) or (>= 60 and <= 70) or (>= 72 and <= 115) or
                118 or 131 or (>= 150 and <= 155) or (>= 163 and <= 164) or
                (>= 166 and <= 168) or 173 or 174 or 188 or 191 or 197 or 198 or 200 or
                (>= 212 and <= 216) or (>= 232 and <= 235) or (>= 247 and <= 250) or
                (>= 253 and <= 255) or (>= 270 and <= 309) => bonus * 5.0,

                _ => 0
            };
        }

        public static double GetSingleUtility(eProperty bonusType, int bonus)
        {
            return GetSingleUtility((int)bonusType, bonus);
        }
    }
}