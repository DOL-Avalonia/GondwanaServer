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
using DOL.Language;
using DOL.Events;

namespace DOL.GS.PlayerTitles
{
    public abstract class TraderTitle : TaskTitle
    {
        public double TradeFactor { get; init; }

        public TraderTitle(int level)
        {
            TradeFactor = -0.05 * level;
        }

        /// <inheritdoc />
        public override void OnTitleSelect(GamePlayer player)
        {
            player.NPCTradeFactor += TradeFactor;
            base.OnTitleSelect(player);
        }

        /// <inheritdoc />
        public override void OnTitleDeselect(GamePlayer player)
        {
            player.NPCTradeFactor -= TradeFactor;
            base.OnTitleDeselect(player);
        }

        /// <inheritdoc />
        public override string GetStatsTranslation(string language)
        {
            return LanguageMgr.GetTranslation(language, "PlayerStatistic.Bonus.TraderTitle", TradeFactor * -100);
        }
    }
    
    [TaskTitleFlag(eTitleFlags.Trader1)]
    public sealed class TraderTitleLevel1 : TraderTitle
    {
        public override string TitleKey => "Titles.Trader.Level1";

        public TraderTitleLevel1() : base(1)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.Trader2)]
    public sealed class TraderTitleLevel2 : TraderTitle
    {
        public override string TitleKey => "Titles.Trader.Level2";

        public TraderTitleLevel2() : base(2)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.Trader3)]
    public sealed class TraderTitleLevel3 : TraderTitle
    {
        public override string TitleKey => "Titles.Trader.Level3";

        public TraderTitleLevel3() : base(3)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.Trader4)]
    public sealed class TraderTitleLevel4 : TraderTitle
    {
        public override string TitleKey => "Titles.Trader.Level4";

        public TraderTitleLevel4() : base(4)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.Trader5)]
    public sealed class TraderTitleLevel5 : TraderTitle
    {
        public override string TitleKey => "Titles.Trader.Level5";

        public TraderTitleLevel5() : base(5)
        {
        }
    }
}