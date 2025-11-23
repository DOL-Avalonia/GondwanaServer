using System;
using DOL.GS.Spells;
using DOL.Events;
using DOL.Database;
using DOL.GS.Utils;

namespace DOL.GS.PropertyCalc
{
    /// <summary>
    /// Calculator for SpellShieldChance
    /// </summary>
    [PropertyCalculator(eProperty.SpellShieldChance)]
    public class SpellShieldChanceCalculator : PropertyCalculator
    {
        public override int CalcValue(GameLiving living, eProperty property)
        {
            int value = living.OtherBuffBonus[eProperty.SpellShieldChance];
            if (living is GamePlayer)
            {
                value += Math.Min(20, living.ItemBonus[(int)property]); // cap 20% from items
            }
            value -= living.DebuffCategory[eProperty.SpellShieldChance];
            return Math.Max(0, value); // Ensuring the gain is not negative
        }
    }

    public class ItemSpellShieldHandler
    {
        private static readonly long CooldownTime = 2 * 60 * 1000;
        private static readonly string LastTriggerPropertyName = "SpellShieldLastTrigger";
        private static readonly string ShuffleBagPropertyName = "SpellShieldShuffleBag";
        private static readonly int SpellID = 20629;

        private static Spell _spellShieldSpell;
        
        public static Spell SpellShieldSpell
        {
            get
            {
                if (_spellShieldSpell == null)
                {
                    DBSpell spell = GameServer.Database.SelectObject<DBSpell>(s => s.SpellID == SpellID);
                    if (spell == null)
                    {
                        spell = new DBSpell
                        {
                            CastTime = 0,
                            ClientEffect = 15217,
                            Icon = 12031,
                            Description = "Absorbs 100% of spell damage when the player's health is at 20% or below.",
                            Name = "Spell Shield",
                            Power = -5,
                            Range = 0,
                            Damage = 0,
                            Value = 15,
                            DamageType = (int)eDamageType.Natural,
                            SpellID = SpellID,
                            Target = "self",
                            Type = "SpellShield",
                            Duration = 90,
                            RecastDelay = (int)CooldownTime, // 2 minutes
                            TooltipId = 6870
                        };
                    }

                    _spellShieldSpell = new Spell(spell, 70);
                    SkillBase.GetSpellList(GlobalSpellsLines.Item_Spells).Add(_spellShieldSpell);
                }
                return _spellShieldSpell;
            }
        }

        public static void ApplyEffect(DOLEvent e, object sender, EventArgs arguments)
        {
            if (arguments is not AttackedByEnemyEventArgs args)
                return;

            GameLiving living = sender as GameLiving;
            if (living == null)
                return;

            if (living.HealthPercent > 30)
                return;
            
            AttackData ad = args.AttackData;
            if (ad is not { AttackType: AttackData.eAttackType.Spell or AttackData.eAttackType.DoT })
                return;

            int chanceToShield = living.GetModified(eProperty.SpellShieldChance);
            if (chanceToShield <= 0)
                return;

            long tick = (long)GameServer.Instance.TickCount;
            if (tick - living.TempProperties.getProperty<long>(LastTriggerPropertyName, -CooldownTime) < CooldownTime)
                return;
            
            if (!living.DrawShuffleBag(ShuffleBagPropertyName, chanceToShield))
                return;

            // Cast the internally defined spell
            Spell spell = SpellShieldSpell;
            SpellLine spellLine = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Spells);

            ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(living, spell, spellLine);
            spellHandler.StartSpell(living);

            // Set the cooldown
            living.TempProperties.setProperty(LastTriggerPropertyName, tick);
        }
    }
}