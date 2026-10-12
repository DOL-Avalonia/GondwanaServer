using System;
using DOL.Database.Attributes;

namespace DOL.Database
{
    /// <summary>
    /// DBDoor is database of door with state of door and X,Y,Z
    /// </summary>
    [DataTable(TableName = "Door")]
    public class DBDoor : DataObject
    {
        private int m_xpos;
        private int m_ypos;
        private int m_zpos;
        private int m_heading;
        private string m_name;
        private int m_type;
        private int m_internalID;
        private byte m_level;
        private byte m_realm;
        private string m_guild;
        private uint m_flags;
        private string m_group_mob_id;
        // private int m_constitution;
        private int m_locked;
        private int m_health;
        private int m_maxHealth;
        private string m_key;
        private short m_key_Chance;
        private bool m_isRenaissance;
        private int m_punishSpell;
        private string m_switchFamily;

        /// <summary>
        /// Create a door row
        /// </summary>
        public DBDoor()
        {
            m_zpos = 0;
            m_ypos = 0;
            m_xpos = 0;
            m_heading = 0;
            m_name = string.Empty;
            m_internalID = 0;
        }

        /// <summary>
        /// Name of door
        /// </summary>
        [DataElement(AllowDbNull = true)]
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
        public int Type
        {
            get
            {
                return m_type;
            }
            set
            {
                Dirty = true;
                m_type = value;
            }
        }

        /// <summary>
        /// Z position of door
        /// </summary>
        [DataElement(AllowDbNull = false)]
        public int Z
        {
            get
            {
                return m_zpos;
            }
            set
            {
                Dirty = true;
                m_zpos = value;
            }
        }

        /// <summary>
        /// Y position of door
        /// </summary>
        [DataElement(AllowDbNull = false)]
        public int Y
        {
            get
            {
                return m_ypos;
            }
            set
            {
                Dirty = true;
                m_ypos = value;
            }
        }

        /// <summary>
        /// X position of door
        /// </summary>
        [DataElement(AllowDbNull = false)]
        public int X
        {
            get
            {
                return m_xpos;
            }
            set
            {
                Dirty = true;
                m_xpos = value;
            }
        }

        /// <summary>
        /// Heading of door
        /// </summary>
        [DataElement(AllowDbNull = false)]
        public int Heading
        {
            get
            {
                return m_heading;
            }
            set
            {
                Dirty = true;
                m_heading = value;
            }
        }

        /// <summary>
        /// Internal index of Door
        /// </summary>
        [DataElement(AllowDbNull = false, Index = true)]
        public int InternalID
        {
            get
            {
                return m_internalID;
            }
            set
            {
                Dirty = true;
                m_internalID = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string Guild
        {
            get
            {
                return m_guild;
            }
            set
            {
                Dirty = true;
                m_guild = value;
            }
        }

        /* 	[DataElement(AllowDbNull = false)]
			public int Constitution
			{
				get
				{
					return m_constitution;
				}
				set
				{
					Dirty = true;
					m_constitution = value;
				}
			}
		 */
        [DataElement(AllowDbNull = false)]
        public byte Level
        {
            get
            {
                return m_level;
            }
            set
            {
                Dirty = true;
                m_level = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public byte Realm
        {
            get
            {
                return m_realm;
            }
            set
            {
                Dirty = true;
                m_realm = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public uint Flags
        {
            get
            {
                return m_flags;
            }
            set
            {
                Dirty = true;
                m_flags = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int Locked
        {
            get
            {
                return m_locked;
            }
            set
            {
                Dirty = true;
                m_locked = value;
            }
        }
        [DataElement(AllowDbNull = false)]
        public int Health
        {
            get
            {
                return m_health;
            }
            set
            {
                Dirty = true;
                m_health = value;
            }
        }
        [DataElement(AllowDbNull = false)]
        public int MaxHealth
        {
            get
            {
                return m_maxHealth;
            }
            set
            {
                Dirty = true;
                m_maxHealth = value;
            }
        }

        /// <summary>
        /// Link a group mob at the door
        /// </summary>
        [DataElement(AllowDbNull = true)]
        public string Group_Mob_Id
        {
            get
            {
                return m_group_mob_id;
            }

            set
            {
                Dirty = true;
                m_group_mob_id = value;
            }
        }

        /// <summary>
        /// Itemtemplate id to open the door
        /// </summary>
        [DataElement(AllowDbNull = true)]
        public string Key
        {
            get
            {
                return m_key;
            }

            set
            {
                Dirty = true;
                m_key = value;
            }
        }

        /// <summary>
        /// Chance of fail to open the door
        /// </summary>
        [DataElement(AllowDbNull = true)]
        public short Key_Chance
        {
            get
            {
                return m_key_Chance;
            }

            set
            {
                Dirty = true;
                m_key_Chance = value;
            }
        }

        /// <summary>
        /// If need the Renaissance state
        /// </summary>
        [DataElement(AllowDbNull = true)]
        public bool IsRenaissance
        {
            get
            {
                return m_isRenaissance;
            }

            set
            {
                Dirty = true;
                m_isRenaissance = value;
            }
        }

        /// <summary>
        /// Spell Id to punish the opener
        /// </summary>
        [DataElement(AllowDbNull = true)]
        public int PunishSpell
        {
            get
            {
                return m_punishSpell;
            }

            set
            {
                Dirty = true;
                m_punishSpell = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string SwitchFamily
        {
            get
            {
                return m_switchFamily;
            }
            set
            {
                Dirty = true;
                m_switchFamily = value;
            }
        }
    }
}
