using System;
using DOL.Events;
using DOL.Language;

namespace DOL.GS.PlayerTitles
{
    public abstract class EruditionTitle : SimplePlayerTitle
    {
        public int Level { get; init; }
        public int CastingSpeedBonus { get; init; }
        public int SpellDamageBonus { get; init; }

        protected EruditionTitle(int level)
        {
            Level = level;

            if (level <= 5)
            {
                CastingSpeedBonus = level;
            }
            else
            {
                CastingSpeedBonus = 5 + ((level - 5) * 2);
            }

            if (level >= 5)
            {
                SpellDamageBonus = Math.Min(5, level - 4);
            }
            else
            {
                SpellDamageBonus = 0;
            }
        }

        /// <summary>
        /// The title automatically becomes available to the player when their EruditionLevel is high enough.
        /// Level 0 is always true, meaning newly created characters automatically have it.
        /// </summary>
        public override bool IsSuitable(GamePlayer player)
        {
            return player.EruditionLevel >= Level;
        }

        /// <summary>
        /// Applies the bonuses when the player equips the title via /title
        /// </summary>
        public override void OnTitleSelect(GamePlayer player)
        {
            if (CastingSpeedBonus > 0)
                player.BaseBuffBonusCategory[eProperty.CastingSpeed] += CastingSpeedBonus;
            
            if (SpellDamageBonus > 0)
                player.BaseBuffBonusCategory[eProperty.SpellDamage] += SpellDamageBonus;

            base.OnTitleSelect(player);
            player.Out.SendCharStatsUpdate();
        }

        /// <summary>
        /// Removes the bonuses when the player takes the title off
        /// </summary>
        public override void OnTitleDeselect(GamePlayer player)
        {
            if (CastingSpeedBonus > 0)
                player.BaseBuffBonusCategory[eProperty.CastingSpeed] -= CastingSpeedBonus;
            
            if (SpellDamageBonus > 0)
                player.BaseBuffBonusCategory[eProperty.SpellDamage] -= SpellDamageBonus;

            base.OnTitleDeselect(player);
            player.Out.SendCharStatsUpdate();
        }

        public override string GetDescription(GamePlayer player)
        {
            return LanguageMgr.TryTranslateOrDefault(player, TitleKey, TitleKey);
        }

        public override string GetValue(GamePlayer source, GamePlayer player)
        {
            return LanguageMgr.TryTranslateOrDefault(source, TitleKey, TitleKey);
        }

        public override string GetStatsTranslation(string language)
        {
            if (Level == 0)
                return LanguageMgr.GetTranslation(language, "PlayerStatistic.Bonus.EruditionTitle0");
            else if (Level < 5)
                return LanguageMgr.GetTranslation(language, "PlayerStatistic.Bonus.EruditionTitleLow", CastingSpeedBonus);
            else
                return LanguageMgr.GetTranslation(language, "PlayerStatistic.Bonus.EruditionTitleHigh", CastingSpeedBonus, SpellDamageBonus);
        }

        public abstract string TitleKey { get; }
    }

    public class EruditionTitleLevel0 : EruditionTitle
    {
        public override string TitleKey => "Titles.Erudition.Level0";
        public EruditionTitleLevel0() : base(0) { }
    }

    public class EruditionTitleLevel1 : EruditionTitle
    {
        public override string TitleKey => "Titles.Erudition.Level1";
        public EruditionTitleLevel1() : base(1) { }
    }

    public class EruditionTitleLevel2 : EruditionTitle
    {
        public override string TitleKey => "Titles.Erudition.Level2";
        public EruditionTitleLevel2() : base(2) { }
    }

    public class EruditionTitleLevel3 : EruditionTitle
    {
        public override string TitleKey => "Titles.Erudition.Level3";
        public EruditionTitleLevel3() : base(3) { }
    }

    public class EruditionTitleLevel4 : EruditionTitle
    {
        public override string TitleKey => "Titles.Erudition.Level4";
        public EruditionTitleLevel4() : base(4) { }
    }

    public class EruditionTitleLevel5 : EruditionTitle
    {
        public override string TitleKey => "Titles.Erudition.Level5";
        public EruditionTitleLevel5() : base(5) { }
    }

    public class EruditionTitleLevel6 : EruditionTitle
    {
        public override string TitleKey => "Titles.Erudition.Level6";
        public EruditionTitleLevel6() : base(6) { }
    }

    public class EruditionTitleLevel7 : EruditionTitle
    {
        public override string TitleKey => "Titles.Erudition.Level7";
        public EruditionTitleLevel7() : base(7) { }
    }

    public class EruditionTitleLevel8 : EruditionTitle
    {
        public override string TitleKey => "Titles.Erudition.Level8";
        public EruditionTitleLevel8() : base(8) { }
    }

    public class EruditionTitleLevel9 : EruditionTitle
    {
        public override string TitleKey => "Titles.Erudition.Level9";
        public EruditionTitleLevel9() : base(9) { }
    }

    public class EruditionTitleLevel10 : EruditionTitle
    {
        public override string TitleKey => "Titles.Erudition.Level10";
        public EruditionTitleLevel10() : base(10) { }
    }
}