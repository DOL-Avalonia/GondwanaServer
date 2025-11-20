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
    public abstract class DuelistTitle : TaskTitle
    {
        public int CritBonus { get; init; }
        public int MeleeWeaponSkillBonus { get; init; }

        protected DuelistTitle(int level)
        {
            CritBonus = level * 4;
            MeleeWeaponSkillBonus = level * 1;
        }

        /// <inheritdoc />
        public override void OnTitleSelect(GamePlayer player)
        {
            player.BaseBuffBonusCategory[eProperty.CriticalSpellHitChance] += CritBonus;
            player.BaseBuffBonusCategory[eProperty.CriticalMeleeHitChance] += CritBonus;
            player.BaseBuffBonusCategory[eProperty.CriticalHealHitChance] += CritBonus;
            player.BaseBuffBonusCategory[eProperty.CriticalArcheryHitChance] += CritBonus;
            player.BaseBuffBonusCategory[eProperty.OffhandDamageAndChanceBonus] += CritBonus;
            player.BaseBuffBonusCategory[eProperty.AllMeleeWeaponSkills] += MeleeWeaponSkillBonus;
            player.BaseBuffBonusCategory[eProperty.AllDualWieldingSkills] += MeleeWeaponSkillBonus;
            base.OnTitleSelect(player);
            player.Out.SendCharStatsUpdate();
            player.Out.SendUpdatePlayerSkills();
        }

        /// <inheritdoc />
        public override void OnTitleDeselect(GamePlayer player)
        {
            player.BaseBuffBonusCategory[eProperty.CriticalSpellHitChance] -= CritBonus;
            player.BaseBuffBonusCategory[eProperty.CriticalMeleeHitChance] -= CritBonus;
            player.BaseBuffBonusCategory[eProperty.CriticalHealHitChance] -= CritBonus;
            player.BaseBuffBonusCategory[eProperty.CriticalArcheryHitChance] -= CritBonus;
            player.BaseBuffBonusCategory[eProperty.OffhandDamageAndChanceBonus] -= CritBonus;
            player.BaseBuffBonusCategory[eProperty.AllMeleeWeaponSkills] -= MeleeWeaponSkillBonus;
            player.BaseBuffBonusCategory[eProperty.AllDualWieldingSkills] -= MeleeWeaponSkillBonus;
            base.OnTitleDeselect(player);
            player.Out.SendCharStatsUpdate();
            player.Out.SendUpdatePlayerSkills();
        }

        /// <inheritdoc />
        public override string GetStatsTranslation(string language)
        {
            return LanguageMgr.GetTranslation(language, "PlayerStatistic.Bonus.DuelistTitle", CritBonus, MeleeWeaponSkillBonus);
        }
    }

    [TaskTitleFlag(eTitleFlags.Duelist1)]
    public class DuelistTitleLevel1 : DuelistTitle
    {
        public override string TitleKey => "Titles.Duelist.Level1";

        /// <inheritdoc />
        public DuelistTitleLevel1() : base(1)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.Duelist2)]
    public class DuelistTitleLevel2 : DuelistTitle
    {
        public override string TitleKey => "Titles.Duelist.Level2";

        /// <inheritdoc />
        public DuelistTitleLevel2() : base(2)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.Duelist3)]
    public class DuelistTitleLevel3 : DuelistTitle
    {
        public override string TitleKey => "Titles.Duelist.Level3";

        /// <inheritdoc />
        public DuelistTitleLevel3() : base(3)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.Duelist4)]
    public class DuelistTitleLevel4 : DuelistTitle
    {
        public override string TitleKey => "Titles.Duelist.Level4";

        /// <inheritdoc />
        public DuelistTitleLevel4() : base(4)
        {
        }
    }
    
    [TaskTitleFlag(eTitleFlags.Duelist5)]
    public class DuelistTitleLevel5 : DuelistTitle
    {
        public override string TitleKey => "Titles.Duelist.Level5";

        /// <inheritdoc />
        public DuelistTitleLevel5() : base(5)
        {
        }
    }
}