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

using DOL.Database;
using DOL.Database.Attributes;

namespace DOL
{
    namespace Database
    {
        [DataTable(TableName = "Zones")]
        public class Zones : DataObject
        {
            private int m_zoneID;
            private string m_zoneName;
            private int m_width;
            private int m_height;
            private int m_offsetY;
            private int m_offsetX;
            private ushort m_regionID;
            private int m_waterLevel;
            private bool m_isLava;
            private byte m_divingFlag = 0;

            private int m_bonusXP;
            private int m_bonusRP;
            private int m_bonusBP;
            private int m_bonusCoin;
            private byte m_realm;
            private bool m_allowMagicalItem;
            private bool m_allowReputation;
            private float m_tensionRate = 1.0f;
            private bool m_isDungeon;

            public Zones()
            {
                m_zoneID = 0;
                m_zoneName = string.Empty;
                m_width = 0;
                m_height = 0;
                m_offsetY = 0;
                m_offsetX = 0;
                m_regionID = 0;
                m_waterLevel = 0;
                m_isLava = false;
                m_divingFlag = 0;

                m_bonusXP = 0;
                m_bonusRP = 0;
                m_bonusBP = 0;
                m_bonusCoin = 0;
                m_realm = 0;
            }

            [PrimaryKey]
            public int ZoneID
            {
                get
                {
                    return m_zoneID;
                }
                set
                {
                    Dirty = true;
                    m_zoneID = value;
                }
            }
            [DataElement(AllowDbNull = false, Index = true)]
            public ushort RegionID
            {
                get
                {
                    return m_regionID;
                }
                set
                {
                    Dirty = true;
                    m_regionID = value;
                }
            }
            [DataElement(AllowDbNull = false)]
            public string Name
            {
                get
                {
                    return m_zoneName;
                }
                set
                {
                    Dirty = true;
                    m_zoneName = value;
                }
            }
            [DataElement(AllowDbNull = false)]
            public bool IsLava
            {
                get
                {
                    return m_isLava;
                }
                set
                {
                    Dirty = true;
                    m_isLava = value;
                }
            }
            /// <summary>
            /// Diving flag for zones to override region.  0 = use region, 1 = Force Yes, 2 = Force No
            /// </summary>
            [DataElement(AllowDbNull = false)]
            public byte DivingFlag
            {
                get
                {
                    return m_divingFlag;
                }
                set
                {
                    Dirty = true;
                    m_divingFlag = value;
                }
            }
            [DataElement(AllowDbNull = false)]
            public int WaterLevel
            {
                get
                {
                    return m_waterLevel;
                }
                set
                {
                    Dirty = true;
                    m_waterLevel = value;
                }
            }
            [DataElement(AllowDbNull = false)]
            public int OffsetY
            {
                get
                {
                    return m_offsetY;
                }
                set
                {
                    Dirty = true;
                    m_offsetY = value;
                }
            }
            [DataElement(AllowDbNull = false)]
            public int OffsetX
            {
                get
                {
                    return m_offsetX;
                }
                set
                {
                    Dirty = true;
                    m_offsetX = value;
                }
            }
            [DataElement(AllowDbNull = false)]
            public int Width
            {
                get
                {
                    return m_width;
                }
                set
                {
                    Dirty = true;
                    m_width = value;
                }
            }
            [DataElement(AllowDbNull = false)]
            public int Height
            {
                get
                {
                    return m_height;
                }
                set
                {
                    Dirty = true;
                    m_height = value;
                }
            }
            [DataElement(AllowDbNull = false)]
            public int Experience
            {
                get
                {
                    return m_bonusXP;
                }
                set
                {
                    Dirty = true;
                    m_bonusXP = value;
                }
            }
            [DataElement(AllowDbNull = false)]
            public int Realmpoints
            {
                get
                {
                    return m_bonusRP;
                }
                set
                {
                    Dirty = true;
                    m_bonusRP = value;
                }
            }
            [DataElement(AllowDbNull = false)]
            public int Bountypoints
            {
                get
                {
                    return m_bonusBP;
                }
                set
                {
                    Dirty = true;
                    m_bonusBP = value;
                }
            }
            [DataElement(AllowDbNull = false)]
            public int Coin
            {
                get
                {
                    return m_bonusCoin;
                }
                set
                {
                    Dirty = true;
                    m_bonusCoin = value;
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
            public bool AllowMagicalItem
            {
                get
                {
                    return m_allowMagicalItem;
                }

                set
                {
                    m_allowMagicalItem = value;
                    Dirty = true;
                }
            }

            [DataElement(AllowDbNull = false)]
            public bool AllowReputation
            {
                get
                {
                    return m_allowReputation;
                }

                set
                {
                    m_allowReputation = value;
                    Dirty = true;
                }
            }

            [DataElement(AllowDbNull = false)]
            public float TensionRate
            {
                get
                {
                    return m_tensionRate;
                }

                set
                {
                    m_tensionRate = value;
                    Dirty = true;
                }
            }

            [DataElement(AllowDbNull = false)]
            public bool IsDungeon
            {
                get
                {
                    return m_isDungeon;
                }

                set
                {
                    m_isDungeon = value;
                    Dirty = true;
                }
            }
        }
    }
}
