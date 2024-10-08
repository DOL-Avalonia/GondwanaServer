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
using DOL.Database.Attributes;

namespace DOL.Database
{
    /// <summary>
    /// Spell Table containing entry for each game SpellHandler behavior.
    /// </summary>
    [DataTable(TableName = "Spell")]
    public class DBSpell : DataObject
    {
        protected int m_spellid;
        protected int m_effectid;
        protected int m_effectLaunch;
        protected int m_effectHit;
        protected int m_icon;
        protected string m_name;
        protected string m_description;
        protected string m_target = string.Empty;

        protected string m_spelltype = string.Empty;
        protected int m_range = 0;
        protected int m_radius = 0;
        protected double m_value = 0;
        protected double m_damage = 0;
        protected int m_damageType = 0;
        protected int m_concentration = 0;
        protected int m_duration = 0;
        protected int m_pulse = 0;
        protected int m_frequency = 0;
        protected int m_pulse_power = 0;
        protected int m_power = 0;
        protected double m_casttime = 0;
        protected int m_recastdelay = 0;
        protected int m_reshealth = 1;
        protected int m_resmana = 0;
        protected int m_lifedrain_return = 0;
        protected int m_amnesia_chance = 0;
        protected string m_message1 = string.Empty;
        protected string m_message2 = string.Empty;
        protected string m_message3 = string.Empty;
        protected string m_message4 = string.Empty;
        protected int m_instrumentRequirement;
        protected int m_spellGroup;
        protected int m_effectGroup;
        protected int m_subSpellID = 0;
        protected int m_subSpellDelay = 0;
        protected bool m_moveCast = false;
        protected bool m_uninterruptible = false;
        protected bool m_isfocus = false;
        protected int m_sharedtimergroup;
        protected string m_packageID = string.Empty;
        protected ushort m_hardTargetCap = 0;

        // warlock
        protected bool m_isprimary;
        protected bool m_issecondary;
        protected bool m_allowbolt;

        // tooltip
        protected ushort m_tooltipId;

        public DBSpell()
        {
            AllowAdd = false;
        }

        [DataElement(AllowDbNull = false, Unique = true)]
        public int SpellID
        {
            get
            {
                return m_spellid;
            }
            set
            {
                Dirty = true;
                m_spellid = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int ClientEffect
        {
            get
            {
                return m_effectid;
            }
            set
            {
                Dirty = true;
                m_effectid = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int ClientLaunchEffect
        {
            get
            {
                return m_effectLaunch;
            }
            set
            {
                Dirty = true;
                m_effectLaunch = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int ClientHitEffect
        {
            get
            {
                return m_effectHit;
            }
            set
            {
                Dirty = true;
                m_effectHit = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int Icon
        {
            get
            {
                return m_icon;
            }
            set
            {
                Dirty = true;
                m_icon = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public string Name
        {
            get
            {
                return m_name;
            }
            set
            {
                Dirty = true;
                m_name = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public string Description
        {
            get
            {
                return m_description;
            }
            set
            {
                Dirty = true;
                m_description = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public string Target
        {
            get
            {
                return m_target;
            }
            set
            {
                Dirty = true;
                m_target = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int Range
        {
            get
            {
                return m_range;
            }
            set
            {
                Dirty = true;
                m_range = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int Power
        {
            get
            {
                return m_power;
            }
            set
            {
                Dirty = true;
                m_power = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public double CastTime
        {
            get
            {
                return m_casttime;
            }
            set
            {
                Dirty = true;
                m_casttime = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public double Damage
        {
            get
            {
                return m_damage;
            }
            set
            {
                Dirty = true;
                m_damage = value;
            }
        }


        [DataElement(AllowDbNull = false)]
        public int DamageType
        {
            get
            {
                return m_damageType;
            }
            set
            {
                Dirty = true;
                m_damageType = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string Type
        {
            get
            {
                return m_spelltype;
            }
            set
            {
                Dirty = true;
                m_spelltype = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int Duration
        {
            get
            {
                return m_duration;
            }
            set
            {
                Dirty = true;
                m_duration = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int Frequency
        {
            get
            {
                return m_frequency;
            }
            set
            {
                Dirty = true;
                m_frequency = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int Pulse
        {
            get
            {
                return m_pulse;
            }
            set
            {
                Dirty = true;
                m_pulse = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int PulsePower
        {
            get
            {
                return m_pulse_power;
            }
            set
            {
                Dirty = true;
                m_pulse_power = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int Radius
        {
            get
            {
                return m_radius;
            }
            set
            {
                Dirty = true;
                m_radius = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public ushort TargetHardCap
        {
            get
            {
                return m_hardTargetCap;
            }
            set
            {
                Dirty = true;
                m_hardTargetCap = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int RecastDelay
        {
            get
            {
                return m_recastdelay;
            }
            set
            {
                Dirty = true;
                m_recastdelay = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int ResurrectHealth
        {
            get
            {
                return m_reshealth;
            }
            set
            {
                Dirty = true;
                m_reshealth = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int ResurrectMana
        {
            get
            {
                return m_resmana;
            }
            set
            {
                Dirty = true;
                m_resmana = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public double Value
        {
            get
            {
                return m_value;
            }
            set
            {
                Dirty = true;
                m_value = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int Concentration
        {
            get
            {
                return m_concentration;
            }
            set
            {
                Dirty = true;
                m_concentration = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int LifeDrainReturn
        {
            get
            {
                return m_lifedrain_return;
            }
            set
            {
                Dirty = true;
                m_lifedrain_return = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int AmnesiaChance
        {
            get
            {
                return m_amnesia_chance;
            }
            set
            {
                Dirty = true;
                m_amnesia_chance = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string Message1
        {
            get { return m_message1; }
            set
            {
                Dirty = true;
                m_message1 = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string Message2
        {
            get { return m_message2; }
            set
            {
                Dirty = true;
                m_message2 = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string Message3
        {
            get { return m_message3; }
            set
            {
                Dirty = true;
                m_message3 = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string Message4
        {
            get { return m_message4; }
            set
            {
                Dirty = true;
                m_message4 = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int InstrumentRequirement
        {
            get { return m_instrumentRequirement; }
            set
            {
                Dirty = true;
                m_instrumentRequirement = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int SpellGroup
        {
            get
            {
                return m_spellGroup;
            }
            set
            {
                Dirty = true;
                m_spellGroup = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int EffectGroup
        {
            get
            {
                return m_effectGroup;
            }
            set
            {
                Dirty = true;
                m_effectGroup = value;
            }
        }

        //Multiple spells
        [DataElement(AllowDbNull = false)]
        public int SubSpellID
        {
            get
            {
                return m_subSpellID;
            }
            set
            {
                Dirty = true;
                m_subSpellID = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int SubSpellDelay
        {
            get
            {
                return m_subSpellDelay;
            }

            set
            {
                Dirty = true;
                m_subSpellDelay = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool MoveCast
        {
            get { return m_moveCast; }
            set
            {
                Dirty = true;
                m_moveCast = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool Uninterruptible
        {
            get { return m_uninterruptible; }
            set
            {
                Dirty = true;
                m_uninterruptible = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool IsFocus
        {
            get { return m_isfocus; }
            set
            {
                Dirty = true;
                m_isfocus = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int SharedTimerGroup
        {
            get
            {
                return m_sharedtimergroup;
            }
            set
            {
                Dirty = true;
                m_sharedtimergroup = value;
            }
        }

        #region warlock
        [DataElement(AllowDbNull = false)]
        public bool IsPrimary
        {
            get
            {
                return (bool)m_isprimary;
            }
            set
            {
                Dirty = true;
                m_isprimary = (bool)value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool IsSecondary
        {
            get
            {
                return (bool)m_issecondary;
            }
            set
            {
                Dirty = true;
                m_issecondary = (bool)value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool AllowBolt
        {
            get
            {
                return (bool)m_allowbolt;
            }
            set
            {
                Dirty = true;
                m_allowbolt = (bool)value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string PackageID
        {
            get
            {
                return this.m_packageID;
            }
            set
            {
                this.m_packageID = value;
                this.Dirty = true;
            }
        }
        #endregion

        [DataElement(AllowDbNull = false)]
        public ushort TooltipId
        {
            get
            {
                return this.m_tooltipId;
            }
            set
            {
                this.m_tooltipId = value;
                this.Dirty = true;
            }
        }

        [Relation(LocalField = nameof(SpellID), RemoteField = nameof(DBSpellXCustomValues.SpellID), AutoLoad = true, AutoDelete = true)]
        public DBSpellXCustomValues[] CustomValues;
    }


    /// <summary>
    /// Spell Custom Values Table containing entries linked to spellID.
    /// </summary>
    [DataTable(TableName = "SpellXCustomValues")]
    public class DBSpellXCustomValues : CustomParam
    {
        private int m_spellID;

        /// <summary>
        /// Spell Table SpellID Reference
        /// </summary>
        [DataElement(AllowDbNull = false, Index = true)]
        public int SpellID
        {
            get { return m_spellID; }
            set { Dirty = true; m_spellID = value; }
        }

        /// <summary>
        /// Create new instance of <see cref="DBSpellXCustomValues"/> linked to Spell ID.
        /// </summary>
        /// <param name="SpellID">Spell ID</param>
        /// <param name="KeyName">Key Name</param>
        /// <param name="Value">Value</param>
        public DBSpellXCustomValues(int SpellID, string KeyName, string Value)
            : base(KeyName, Value)
        {
            this.SpellID = SpellID;
        }

        /// <summary>
        /// Create new instance of <see cref="DBSpellXCustomValues"/>
        /// </summary>
        public DBSpellXCustomValues()
        {
        }

    }

}
