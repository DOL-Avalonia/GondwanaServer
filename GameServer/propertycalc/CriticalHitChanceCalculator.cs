using System;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.Spells;

namespace DOL.GS.PropertyCalc
{
    /// <summary>
    /// The critical hit chance calculator. Returns 0 .. 100 chance.
    /// 
    /// BuffBonusCategory1 unused
    /// BuffBonusCategory2 unused
    /// BuffBonusCategory3 unused
    /// BuffBonusCategory4 for uncapped realm ability bonus
    /// BuffBonusMultCategory1 unused
    /// </summary>
    [PropertyCalculator(eProperty.CriticalArcheryHitChance)]
    public class CriticalArcheryHitChanceCalculator : PropertyCalculator
    {
        public CriticalArcheryHitChanceCalculator() { }

        public override int CalcValue(GameLiving living, eProperty property)
        {
            int chance = living.BaseBuffBonusCategory[(int)property] + living.OtherBuffBonus[(int)property] + living.AbilityBonus[(int)property] + living.ItemBonus[(int)property];

            if (living is GamePet gamePet)
            {
                if (ServerProperties.Properties.EXPAND_WILD_MINION && gamePet.Brain is IControlledBrain playerBrain
                    && playerBrain.Owner is GamePlayer player
                    && player.GetAbility<RealmAbilities.WildMinionAbility>() is RealmAbilities.WildMinionAbility ab)
                    chance += ab.Amount;
            }
            else // not a pet
                chance += 10;

            // Base cap
            chance = Math.Min(chance, 50);
            chance += living.BuffBonusUncapped[(int)property];

            // ultimate cap of 100%
            return Math.Min(chance, 100);
        }
    }
    
    /// <summary>
    /// The critical hit chance calculator. Returns 0 .. 100 chance.
    /// 
    /// BuffBonusCategory1 unused
    /// BuffBonusCategory2 unused
    /// BuffBonusCategory3 unused
    /// BuffBonusCategory4 unused
    /// BuffBonusMultCategory1 unused
    /// AbilityBonus used
    /// </summary>
    [PropertyCalculator(eProperty.CriticalSpellHitChance)]
    public class CriticalSpellHitChanceCalculator : PropertyCalculator
    {
        public CriticalSpellHitChanceCalculator() { }

        public override int CalcValue(GameLiving living, eProperty property)
        {
            int chance = living.BaseBuffBonusCategory[(int)property] + living.OtherBuffBonus[(int)property] + living.AbilityBonus[(int)property] + living.ItemBonus[(int)property];

            if (living is GamePlayer player)
            {
                if (player.CharacterClass.ClassType == eClassType.ListCaster)
                    chance += 10;
            }
            else if (living is NecromancerPet petNecro)
            {
                if (petNecro.Brain is IControlledBrain brainNecro && brainNecro.Owner is GamePlayer necro
                    && necro.GetAbility<RealmAbilities.WildPowerAbility>() is RealmAbilities.WildPowerAbility raWP)
                    chance += raWP.Amount;
            }
            else if (living is GamePet pet)
            {
                if (ServerProperties.Properties.EXPAND_WILD_MINION
                    && pet.Brain is IControlledBrain brainPet && brainPet.Owner is GamePlayer playerOwner
                    && playerOwner.GetAbility<RealmAbilities.WildMinionAbility>() is RealmAbilities.WildMinionAbility raWM)
                    chance += raWM.Amount;
            }

            // Base cap
            chance = Math.Min(chance, 50);
            chance += living.BuffBonusUncapped[(int)property];

            // ultimate cap of 100%
            return Math.Min(chance, 100);
        }
    }
    
    /// <summary>
    /// The critical hit chance calculator. Returns 0 .. 100 chance.
    ///
    /// BuffBonusCategory1 unused
    /// BuffBonusCategory2 unused
    /// BuffBonusCategory3 unused
    /// BuffBonusCategory4 for uncapped realm ability bonus
    /// BuffBonusMultCategory1 unused
    ///
    /// Crit propability is capped to 50% except for berserk
    /// </summary>
    [PropertyCalculator(eProperty.CriticalMeleeHitChance)]
    public class CriticalMeleeHitChanceCalculator : PropertyCalculator
    {
        public CriticalMeleeHitChanceCalculator() { }

        public override int CalcValue(GameLiving living, eProperty property)
        {
            // no berserk for ranged weapons
            IGameEffect berserk = living.EffectList.GetOfType<BerserkEffect>();
            if (berserk != null)
            {
                return 100;
            }

            // base 10% chance of critical for all with melee weapons plus ra bonus
            int chance = living.BaseBuffBonusCategory[(int)property] + living.OtherBuffBonus[(int)property] + living.AbilityBonus[(int)property] + living.ItemBonus[(int)property];

            if (living is NecromancerPet necroPet)
            {
                if (necroPet.Brain is IControlledBrain necroPetBrain && necroPetBrain.Owner is GamePlayer necro
                    && necro.GetAbility<RealmAbilities.MasteryOfPain>() is RealmAbilities.MasteryOfPain raMoP)
                    chance += raMoP.Amount;
            }
            else if (living is GamePet pet)
            {
                if (pet.Brain is IControlledBrain petBrain && petBrain.Owner is GamePlayer player
                    && player.GetAbility<RealmAbilities.WildMinionAbility>() is RealmAbilities.WildMinionAbility raWM)
                    chance += raWM.Amount;
            }
            else // not a pet
                chance += 10;

            // Base cap
            chance = Math.Min(chance, 50);
            chance += living.BuffBonusUncapped[(int)property];

            // ultimate cap of 100%
            return Math.Min(chance, 100);
        }
    }
    
    /// <summary>
    /// The critical hit chance calculator. Returns 0 .. 100 chance.
    ///
    /// BuffBonusCategory1 unused
    /// BuffBonusCategory2 unused
    /// BuffBonusCategory3 unused
    /// BuffBonusCategory4 for uncapped realm ability bonus
    /// BuffBonusMultCategory1 unused
    ///
    /// Crit propability is capped to 50% except for berserk
    /// </summary>
    [PropertyCalculator(eProperty.CriticalDotHitChance)]
    public class CriticalDotHitChanceCalculator : PropertyCalculator
    {
        public CriticalDotHitChanceCalculator() { }

        public override int CalcValue(GameLiving living, eProperty property)
        {
            int chance = living.BaseBuffBonusCategory[(int)property] + living.OtherBuffBonus[(int)property] + living.AbilityBonus[(int)property] + living.ItemBonus[(int)property];

            if (living is GamePlayer player)
            {
                if (player.CharacterClass.ClassType == eClassType.ListCaster)
                    chance += 10;
            }
            else if (living is NecromancerPet petNecro)
            {
                if (petNecro.Brain is IControlledBrain brainNecro && brainNecro.Owner is GamePlayer necro
                    && necro.GetAbility<RealmAbilities.WildPowerAbility>() is { } raWP)
                    chance += raWP.Amount;
            }
            else if (living is GamePet pet)
            {
                if (ServerProperties.Properties.EXPAND_WILD_MINION
                    && pet.Brain is IControlledBrain brainPet && brainPet.Owner is GamePlayer playerOwner
                    && playerOwner.GetAbility<RealmAbilities.WildMinionAbility>() is { } raWM)
                    chance += raWM.Amount;
            }

            // Base cap
            chance = Math.Min(chance, 50);
            chance += living.BuffBonusUncapped[(int)property];

            // ultimate cap of 100%
            return Math.Min(chance, 100);
        }
    }
}
