using System;
namespace DOL.GS.PropertyCalc
{
    [PropertyCalculator(eProperty.WeaponSkill)]
    public class WeaponSkillPercentCalculator : PropertyCalculator
    {
        public override int CalcValue(GameLiving living, eProperty property)
        {
            double percent = 100;

            percent += living.ItemBonus[(int)property];
            percent += living.BaseBuffBonusCategory[(int)property]; // enchance the weaponskill
            percent += living.SpecBuffBonusCategory[(int)property]; // enchance the weaponskill
            //hotfix for poisons where both debuff components have same value
            percent -= (int)Math.Abs(living.DebuffCategory[(int)property] / 5.4); // reduce
            return (int)Math.Max(1, percent);
        }

        /// <inheritdoc />
        public override int CalcValueBase(GameLiving living, eProperty property)
        {
            double percent = 100 + living.ItemBonus[(int)property];
            return (int)Math.Max(1, percent);
        }
    }
}
