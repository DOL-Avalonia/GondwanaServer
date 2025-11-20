/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */

using System;
using DOL.Events;
using DOL.Language;

namespace DOL.GS.PlayerTitles
{
    public abstract class BountyhunterTitle : TaskTitle
    {
        public int RobberyResistBonus { get; init; }
        public int StealthDetectionBonus { get; init; }
        public double TradeFactor { get; init; }

        protected BountyhunterTitle(int level)
        {
            RobberyResistBonus = 5 * level;
            StealthDetectionBonus = 2 * level;
            TradeFactor = -0.01 * level;
        }

        /// <inheritdoc />
        public override void OnTitleSelect(GamePlayer player)
        {
            player.BaseBuffBonusCategory[eProperty.RobberyResist] += RobberyResistBonus;
            player.BaseBuffBonusCategory[eProperty.StealthDetectionBonus] += StealthDetectionBonus;
            player.NPCTradeFactor += TradeFactor;
            base.OnTitleSelect(player);
        }

        /// <inheritdoc />
        public override void OnTitleDeselect(GamePlayer player)
        {
            player.BaseBuffBonusCategory[eProperty.RobberyResist] -= RobberyResistBonus;
            player.BaseBuffBonusCategory[eProperty.StealthDetectionBonus] -= StealthDetectionBonus;
            player.NPCTradeFactor -= TradeFactor;
            base.OnTitleDeselect(player);
        }

        /// <inheritdoc />
        public override string GetStatsTranslation(string language)
        {
            return LanguageMgr.GetTranslation(language, "PlayerStatistic.Bonus.BountyhunterTitle", RobberyResistBonus, StealthDetectionBonus, TradeFactor * -100);
        }
    }

    [TaskTitleFlag(eTitleFlags.BountyHunter1)]
    public class BountyhunterTitleLevel1 : BountyhunterTitle
    {
        public override string TitleKey => "Titles.Bountyhunter.Level1";

        public BountyhunterTitleLevel1() : base(1)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.BountyHunter2)]
    public class BountyhunterTitleLevel2 : BountyhunterTitle
    {
        public override string TitleKey => "Titles.Bountyhunter.Level2";

        public BountyhunterTitleLevel2() : base(2)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.BountyHunter3)]
    public class BountyhunterTitleLevel3 : BountyhunterTitle
    {
        public override string TitleKey => "Titles.Bountyhunter.Level3";

        public BountyhunterTitleLevel3() : base(3)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.BountyHunter4)]
    public class BountyhunterTitleLevel4 : BountyhunterTitle
    {
        public override string TitleKey => "Titles.Bountyhunter.Level4";

        public BountyhunterTitleLevel4() : base(4)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.BountyHunter5)]
    public class BountyhunterTitleLevel5 : BountyhunterTitle
    {
        public override string TitleKey => "Titles.Bountyhunter.Level5";

        public BountyhunterTitleLevel5() : base(5)
        {
        }
    }
}