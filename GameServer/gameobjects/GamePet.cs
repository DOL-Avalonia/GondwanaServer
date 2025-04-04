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
using System.Collections.Generic;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.Effects;
using DOL.Events;
using DOL.GS.ServerProperties;
using DOL.GS.Spells;
using DOL.GS.Styles;
using DOL.GS.Utils;
using DOL.AI;

namespace DOL.GS
{
    public class GamePet : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        public GamePet(INpcTemplate template) : base(template)
        {
            if (Inventory != null)
            {
                if (Inventory.GetItem(eInventorySlot.DistanceWeapon) != null)
                    SwitchWeapon(GameLiving.eActiveWeaponSlot.Distance);
                else if (Inventory.GetItem(eInventorySlot.RightHandWeapon) != null)
                    SwitchWeapon(GameLiving.eActiveWeaponSlot.Standard);
                else if (Inventory.GetItem(eInventorySlot.TwoHandWeapon) != null)
                    SwitchWeapon(GameLiving.eActiveWeaponSlot.TwoHanded);
            }
            AddStatsToWeapon();
            BroadcastLivingEquipmentUpdate();
        }

        public GamePet(ABrain brain) : base(brain)
        {

        }

        /// <summary>
        /// Gets or sets the level of this NPC
        /// </summary>
        public override byte Level
        {
            get { return base.Level; }
            set
            {
                // Don't set the pet level until the owner is set
                // This skips unnecessary calls to code in base.Level
                if (Owner != null)
                {
                    base.Level = value;
                    SortSpells();
                }
            }
        }

        // Store the info we need from the summoning spell to calculate pet level.
        public double SummonSpellDamage { get; set; } = -88.0;
        public double SummonSpellValue { get; set; } = 44.0;

        /// <summary>
        /// Set the pet's level based on owner's level.  Make sure Owner brain has been assigned before calling!
        /// </summary>
        /// <returns>Did the pet's level change?</returns>
        public virtual bool SetPetLevel()
        {
            // Changing Level calls additional code, so only do it at the end
            byte newLevel = 0;

            if (SummonSpellDamage >= 0)
                newLevel = (byte)SummonSpellDamage;
            else if (!(Owner is GamePet))
                newLevel = (byte)(Owner.Level * SummonSpellDamage * -0.01);
            else if (GetLivingOwner() is GameLiving summoner)
                newLevel = (byte)(summoner.Level * SummonSpellDamage * -0.01);

            if (SummonSpellValue > 0 && newLevel > SummonSpellValue)
                newLevel = (byte)SummonSpellValue;

            if (newLevel < 1)
                newLevel = 1;

            if (Level == newLevel)
                return false;

            Level = newLevel;
            return true;
        }

        public override int Mana { get => 5000; set => base.Mana = value; }
        public override int MaxMana => 5000;

        #region Inventory

        /// <summary>
        /// Load equipment for the pet.
        /// </summary>
        /// <param name="templateID">Equipment Template ID.</param>
        /// <returns>True on success, else false.</returns>
        protected virtual void AddStatsToWeapon()
        {
            if (Inventory != null)
            {
                InventoryItem item;
                if ((item = Inventory.GetItem(eInventorySlot.TwoHandWeapon)) != null)
                {
                    item.DPS_AF = (int)(Level * 3.3);
                    item.SPD_ABS = 50;
                }
                if ((item = Inventory.GetItem(eInventorySlot.RightHandWeapon)) != null)
                {
                    item.DPS_AF = (int)(Level * 3.3);
                    item.SPD_ABS = 37;
                }
                if ((item = Inventory.GetItem(eInventorySlot.LeftHandWeapon)) != null)
                {
                    item.DPS_AF = (int)(Level * 3.3);
                    item.SPD_ABS = 50;
                }
                if ((item = Inventory.GetItem(eInventorySlot.DistanceWeapon)) != null)
                {
                    item.DPS_AF = (int)(Level * 3.3);
                    item.SPD_ABS = 50;
                    SwitchWeapon(eActiveWeaponSlot.Distance);
                    BroadcastLivingEquipmentUpdate();
                }
            }
        }

        #endregion

        #region Shared Melee & Spells

        /// <summary>
        /// Multiplier for melee and magic.
        /// </summary>
        public override double Effectiveness
        {
            get
            {
                return (GetLivingOwner()?.Effectiveness ?? 1.0) * base.Effectiveness;
            }
        }
        #endregion

        #region Spells

        /// <summary>
        /// Sort spells into specific lists
        /// </summary>
        public override void SortSpells()
        {
            SortSpells(0);
        }

        /// <summary>
        /// Sort spells into specific lists, scaling spells by scaleLevel
        /// </summary>
        /// <param name="casterLevel">The level to scale the pet spell to, 0 to use pet level</param>
        public virtual void SortSpells(int scaleLevel)
        {
            if (Spells.Count < 1 || Level < 1)
                return;

            if (DOL.GS.ServerProperties.Properties.PET_SCALE_SPELL_MAX_LEVEL <= 0)
                base.SortSpells();
            else
            {
                if (scaleLevel <= 0)
                    scaleLevel = Level;

                if (DOL.GS.ServerProperties.Properties.PET_LEVELS_WITH_OWNER ||
                    (this is BDSubPet && DOL.GS.ServerProperties.Properties.PET_CAP_BD_MINION_SPELL_SCALING_BY_SPEC))
                {
                    // We'll need to be able to scale spells for this pet multiple times, so we
                    //	need to keep the original spells in Spells and only scale sorted copies.

                    base.SortSpells();

                    if (CanCastHarmfulSpells)
                        for (int i = 0; i < HarmfulSpells.Count; i++)
                        {
                            HarmfulSpells[i] = HarmfulSpells[i].Copy();
                            ScalePetSpell(HarmfulSpells[i], scaleLevel);
                        }

                    if (CanCastInstantHarmfulSpells)
                        for (int i = 0; i < InstantHarmfulSpells.Count; i++)
                        {
                            InstantHarmfulSpells[i] = InstantHarmfulSpells[i].Copy();
                            ScalePetSpell(InstantHarmfulSpells[i], scaleLevel);
                        }

                    if (CanCastHealSpells)
                        for (int i = 0; i < HealSpells.Count; i++)
                        {
                            HealSpells[i] = HealSpells[i].Copy();
                            ScalePetSpell(HealSpells[i], scaleLevel);
                        }

                    if (CanCastInstantHealSpells)
                        for (int i = 0; i < InstantHealSpells.Count; i++)
                        {
                            InstantHealSpells[i] = InstantHealSpells[i].Copy();
                            ScalePetSpell(InstantHealSpells[i], scaleLevel);
                        }

                    if (CanCastInstantMiscSpells)
                        for (int i = 0; i < InstantMiscSpells.Count; i++)
                        {
                            InstantMiscSpells[i] = InstantMiscSpells[i].Copy();
                            ScalePetSpell(InstantMiscSpells[i], scaleLevel);
                        }

                    if (CanCastMiscSpells)
                        for (int i = 0; i < MiscSpells.Count; i++)
                        {
                            MiscSpells[i] = MiscSpells[i].Copy();
                            ScalePetSpell(MiscSpells[i], scaleLevel);
                        }
                }
                else
                {
                    // We don't need to keep the original spells, so don't waste memory keeping separate copies.
                    foreach (Spell spell in Spells)
                        ScalePetSpell(spell, scaleLevel);

                    base.SortSpells();
                }
            }
        }

        /// <summary>
        /// Can this living cast the given spell while in combat?
        /// </summary>
        /// <param name="spell"></param>
        /// <returns></returns>
        public override bool CanCastInCombat(Spell spell)
        {
            return spell == null || spell.IsInstantCast || spell.Uninterruptible;
        }

        /// <summary>
        /// Called when spell has finished casting.
        /// </summary>
        /// <param name="handler"></param>
        public override void OnAfterSpellCastSequence(ISpellHandler handler)
        {
            base.OnAfterSpellCastSequence(handler);
            Brain.Notify(GameNPCEvent.CastFinished, this, new CastingEventArgs(handler));
        }

        /// <summary>
        /// Scale the passed spell according to PET_SCALE_SPELL_MAX_LEVEL
        /// </summary>
        /// <param name="spell">The spell to scale</param>
        /// <param name="casterLevel">The level to scale the pet spell to, 0 to use pet level</param>
        public virtual void ScalePetSpell(Spell spell, int casterLevel = 0)
        {
            if (ServerProperties.Properties.PET_SCALE_SPELL_MAX_LEVEL <= 0 || spell == null || Level < 1)
                return;

            if (casterLevel < 1)
                casterLevel = Level;

            switch (spell.SpellType.ToString().ToLower())
            {
                // Scale Damage
                case "damageovertime":
                case "damageshield":
                case "damageadd":
                case "directdamage":
                case "lifedrain":
                case "damagespeeddecrease":
                case "stylebleeding": // Style bleed effect
                    spell.Damage *= (double)casterLevel / ServerProperties.Properties.PET_SCALE_SPELL_MAX_LEVEL;
                    break;
                // Scale Value
                case "enduranceregenbuff":
                case "enduranceheal":
                case "endurancedrain":
                case "powerregenbuff":
                case "powerheal":
                case "powerdrain":
                case "powerhealthenduranceregenbuff":
                case "combatspeedbuff":
                case "hastebuff":
                case "celeritybuff":
                case "combatspeeddebuff":
                case "hastedebuff":
                case "heal":
                case "combatheal":
                case "healthregenbuff":
                case "healovertime":
                case "constitutionbuff":
                case "dexteritybuff":
                case "strengthbuff":
                case "constitutiondebuff":
                case "dexteritydebuff":
                case "strengthdebuff":
                case "armorfactordebuff":
                case "armorfactorbuff":
                case "armorabsorptionbuff":
                case "armorabsorptiondebuff":
                case "dexterityquicknessbuff":
                case "strengthconstitutionbuff":
                case "dexterityquicknessdebuff":
                case "strengthconstitutiondebuff":
                case "taunt":
                case "unbreakablespeeddecrease":
                case "speeddecrease":
                case "stylecombatspeeddebuff": // Style attack speed debuff
                    spell.Value *= (double)casterLevel / ServerProperties.Properties.PET_SCALE_SPELL_MAX_LEVEL;
                    break;
                // Scale Duration
                case "disease":
                case "stun":
                case "unrresistablenonimunitystun":
                case "mesmerize":
                case "stylestun": // Style stun effect
                case "stylespeeddecrease": // Style hinder effect
                    spell.Duration = (int)Math.Ceiling(spell.Duration * (double)casterLevel / ServerProperties.Properties.PET_SCALE_SPELL_MAX_LEVEL);
                    break;
                // Scale Damage and value
                case "directdamagewithdebuff":
                    /* Patch 1.123: For Cabalist, Enchanter, and Spiritmaster pets
					 * The debuff component of its nuke has been as follows:
					 *	For pet level 1-23, the debuff is now 10%.
					 *	For pet level 24-43, the debuff is now 20%.
					 *	For pet level 44-50, the debuff is now 30%. */
                    spell.Value *= (double)casterLevel / ServerProperties.Properties.PET_SCALE_SPELL_MAX_LEVEL;
                    spell.Duration = (int)Math.Ceiling(spell.Duration * (double)casterLevel / ServerProperties.Properties.PET_SCALE_SPELL_MAX_LEVEL);
                    break;
                case "styletaunt": // Style taunt effects already scale with damage
                case "curepoison":
                case "curedisease":
                    break;
                default:
                    break; // Don't mess with types we don't know
            } // switch (m_spell.SpellType.ToString().ToLower())
        }

        #endregion

        #region Stats
        /// <summary>
        /// Set stats according to PET_AUTOSET values, then scale them according to the npcTemplate
        /// </summary>
        public override void AutoSetStats()
        {
            // Assign base values
            Strength = Math.Max((short)1, Properties.PET_AUTOSET_STR_BASE);
            Constitution = Math.Max((short)1, Properties.PET_AUTOSET_CON_BASE);
            Quickness = Math.Max((short)1, Properties.PET_AUTOSET_QUI_BASE);
            Dexterity = Math.Max((short)1, Properties.PET_AUTOSET_DEX_BASE);
            Intelligence = Math.Max((short)1, Properties.PET_AUTOSET_INT_BASE);
            Empathy = Math.Max((short)1, Properties.PET_AUTOSET_INT_BASE);
            Piety = Math.Max((short)1, Properties.PET_AUTOSET_INT_BASE);
            Charisma = Math.Max((short)1, Properties.PET_AUTOSET_INT_BASE);

            if (Level > 1)
            {
                // Now add stats for levelling
                Strength += (short)Math.Round((Level - 1) * Properties.PET_AUTOSET_STR_MULTIPLIER);
                Constitution += (short)Math.Round((Level - 1) * Properties.PET_AUTOSET_CON_MULTIPLIER);
                Quickness += (short)Math.Round((Level - 1) * Properties.PET_AUTOSET_QUI_MULTIPLIER);
                Dexterity += (short)Math.Round((Level - 1) * Properties.PET_AUTOSET_DEX_MULTIPLIER);
                Intelligence += (short)Math.Round((Level - 1) * Properties.PET_AUTOSET_INT_MULTIPLIER);
                Empathy += (short)Math.Round((Level - 1) * Properties.PET_AUTOSET_INT_MULTIPLIER);
                Piety += (short)Math.Round((Level - 1) * Properties.PET_AUTOSET_INT_MULTIPLIER);
                Charisma += (short)Math.Round((Level - 1) * Properties.PET_AUTOSET_INT_MULTIPLIER);
                WeaponDps = (int)((1.4 + 0.3 * Level + Level * Level * 0.002) * 10);
                WeaponSpd = 30;
                ArmorFactor = (int)((1.0 + (Level / 100.0)) * Level * 1.8);
                ArmorAbsorb = (int)((Level - 10) * 0.5 - (Level - 60) * Level * 0.0015).Clamp(0, 75);
            }

            // Now scale them according to NPCTemplate values
            if (NPCTemplate != null)
            {
                if (NPCTemplate.Strength > 0)
                    Strength = NPCTemplate.Strength;
                if (NPCTemplate.Constitution > 0)
                    Constitution = NPCTemplate.Constitution;
                if (NPCTemplate.Quickness > 0)
                    Quickness = NPCTemplate.Quickness;
                if (NPCTemplate.Dexterity > 0)
                    Dexterity = NPCTemplate.Dexterity;
                if (NPCTemplate.Intelligence > 0)
                    Intelligence = NPCTemplate.Intelligence;
                if (NPCTemplate.Empathy > 0)
                    Empathy = NPCTemplate.Empathy;
                if (NPCTemplate.Piety > 0)
                    Piety = NPCTemplate.Piety;
                if (NPCTemplate.Charisma > 0)
                    Charisma = NPCTemplate.Charisma;
                if (NPCTemplate.WeaponDps > 0)
                    WeaponDps = NPCTemplate.WeaponDps;
                if (NPCTemplate.WeaponSpd > 0)
                    WeaponSpd = NPCTemplate.WeaponSpd;
                if (NPCTemplate.ArmorFactor > 0)
                    ArmorFactor = NPCTemplate.ArmorFactor;
                if (NPCTemplate.ArmorAbsorb > 0)
                    ArmorAbsorb = NPCTemplate.ArmorAbsorb;

                // scaling
                if (NPCTemplate.Strength < 0)
                    Strength = (short)(NPCTemplate.Strength * -(Level + 1));
                if (NPCTemplate.Constitution < 0)
                    Constitution = (short)(NPCTemplate.Constitution * -(Level + 1));
                if (NPCTemplate.Quickness < 0)
                    Quickness = (short)(NPCTemplate.Quickness * -(Level + 1));
                if (NPCTemplate.Dexterity < 0)
                    Dexterity = (short)(NPCTemplate.Dexterity * -(Level + 1));
                if (NPCTemplate.Intelligence < 0)
                    Intelligence = (short)(NPCTemplate.Intelligence * -(Level + 1));
                if (NPCTemplate.Empathy < 0)
                    Empathy = (short)(NPCTemplate.Empathy * -(Level + 1));
                if (NPCTemplate.Piety < 0)
                    Piety = (short)(NPCTemplate.Piety * -(Level + 1));
                if (NPCTemplate.Charisma < 0)
                    Charisma = (short)(NPCTemplate.Charisma * -(Level + 1));
                if (NPCTemplate.WeaponDps < 0)
                    WeaponDps = NPCTemplate.WeaponDps * -(Level + 1);
                if (NPCTemplate.WeaponSpd < 0)
                    WeaponSpd = NPCTemplate.WeaponSpd * -(Level + 1);
                if (NPCTemplate.ArmorFactor < 0)
                    ArmorFactor = NPCTemplate.ArmorFactor * -(Level + 1);
                if (NPCTemplate.ArmorAbsorb < 0)
                    ArmorAbsorb = NPCTemplate.ArmorAbsorb * -(Level + 1);

            }
        }
        #endregion

        #region Melee

        /// <summary>
        /// The type of damage the currently active weapon does.
        /// </summary>
        /// <param name="weapon"></param>
        /// <returns></returns>
        public override eDamageType AttackDamageType(InventoryItem weapon)
        {
            if (weapon != null)
            {
                switch ((eWeaponDamageType)weapon.Type_Damage)
                {
                    case eWeaponDamageType.Crush: return eDamageType.Crush;
                    case eWeaponDamageType.Slash: return eDamageType.Slash;
                    case eWeaponDamageType.Thrust: return eDamageType.Thrust;
                }
            }

            return base.AttackDamageType(weapon);
        }

        /// <summary>
        /// Get melee speed in milliseconds.
        /// </summary>
        /// <param name="weapons"></param>
        /// <returns></returns>
        public override int AttackSpeed(params InventoryItem[] weapons)
        {
            double weaponSpeed = 0.0;

            if (weapons != null)
            {
                foreach (InventoryItem item in weapons)
                {
                    if (item != null)
                    {
                        weaponSpeed += item.SPD_ABS;
                    }
                    else
                    {
                        weaponSpeed += 34;
                    }
                }

                weaponSpeed = (weapons.Length > 0) ? weaponSpeed / weapons.Length : 34.0;
            }
            else
            {
                weaponSpeed = 34.0;
            }

            double speed = 100 * weaponSpeed * (1.0 - (GetModified(eProperty.Quickness) - 60) / 500.0);
            return (int)Math.Max(500.0, (speed * (double)GetModified(eProperty.MeleeSpeed) * 0.01)); // no bonus is 100%, opposite how players work
        }

        /// <summary>
        /// Calculate how fast this pet can cast a given spell
        /// </summary>
        /// <param name="spell"></param>
        /// <returns></returns>
        public override int CalculateCastingTime(SpellLine line, Spell spell)
        {
            int ticks = spell.CastTime;

            double percent = DexterityCastTimeReduction;
            percent -= GetModified(eProperty.CastingSpeed) * .01;

            ticks = (int)(ticks * Math.Max(CastingSpeedReductionCap, percent));
            if (ticks < MinimumCastingSpeed)
                ticks = MinimumCastingSpeed;

            return ticks;
        }
        #endregion

        public override void Die(GameObject killer)
        {
            StripBuffs();

            GameEventMgr.Notify(GameLivingEvent.PetReleased, this);
            base.Die(killer);
            CurrentRegion = null;
        }

        /// <summary>
        /// Targets the pet has buffed, to allow correct buff removal when the pet dies
        /// </summary>
        private List<GameLiving> m_buffedTargets = null;

        /// <summary>
        /// Add a target to the pet's list of buffed targets
        /// </summary>
        /// <param name="living">Target to add to the list</param>
        public void AddBuffedTarget(GameLiving living)
        {
            if (living == this)
                return;

            if (m_buffedTargets == null)
                m_buffedTargets = new List<GameLiving>(1);

            if (!m_buffedTargets.Contains(living))
                m_buffedTargets.Add(living);
        }

        /// <summary>
        /// Strips any buffs this pet cast
        /// </summary>
        public virtual void StripBuffs()
        {
            if (m_buffedTargets != null)
                foreach (GameLiving living in m_buffedTargets)
                    if (living != this && living.EffectList != null)
                        foreach (IGameEffect effect in living.EffectList)
                            if (effect is GameSpellEffect spellEffect && spellEffect.SpellHandler != null
                                && spellEffect.SpellHandler.Caster != null && spellEffect.SpellHandler.Caster == this)
                                effect.Cancel(false);
        }

        /// <summary>
        /// Spawn texts are in database
        /// </summary>
        public override void BuildAmbientTexts()
        {
            base.BuildAmbientTexts();

            // also add the pet specific ambient texts if none found
            if (ambientTexts.Count == 0)
                ambientTexts = GameServer.Instance.NpcManager.AmbientBehaviour["pet"];
        }

        public override bool IsObjectGreyCon(GameObject obj)
        {
            if (obj.GetLivingOwner() is { } owner)
            {
                obj = owner;
            }
            return base.IsObjectGreyCon(obj);
        }
    }
}
