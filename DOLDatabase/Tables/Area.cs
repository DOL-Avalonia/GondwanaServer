using System;
using DOL.Database;
using DOL.Database.Attributes;

namespace DOL.Database
{
    [DataTable(TableName = "Area")]
    public class DBArea : DataObject
    {
        private string m_translationId;
        private string m_description;
        private int m_x;
        private int m_y;
        private int m_z;
        private int m_radius;
        private ushort m_region;
        private string m_classType = string.Empty;
        private bool m_canBroadcast;
        private byte m_sound;
        private bool m_checkLOS;
        private string m_points;
        private bool m_allowVol;
        private bool m_safeArea;
        private int realmPoints;
        private bool m_isPvp;

        public DBArea()
        {
            this.m_allowVol = true;
        }

        [DataElement(AllowDbNull = true)]
        public string TranslationId
        {
            get { return m_translationId; }
            set
            {
                Dirty = true;
                m_translationId = value;
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
        public int X
        {
            get
            {
                return m_x;
            }
            set
            {
                Dirty = true;
                m_x = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int Y
        {
            get
            {
                return m_y;
            }
            set
            {
                Dirty = true;
                m_y = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int Z
        {
            get
            {
                return m_z;
            }
            set
            {
                Dirty = true;
                m_z = value;
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
        public ushort Region
        {
            get
            {
                return m_region;
            }
            set
            {
                Dirty = true;
                m_region = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string ClassType
        {
            get
            {
                return m_classType;
            }
            set
            {
                Dirty = true;
                m_classType = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool CanBroadcast
        {
            get
            {
                return m_canBroadcast;
            }
            set
            {
                Dirty = true;
                m_canBroadcast = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool AllowVol
        {
            get
            {
                return m_allowVol;
            }

            set
            {
                Dirty = true;
                m_allowVol = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool SafeArea
        {
            get
            {
                return m_safeArea;
            }

            set
            {
                Dirty = true;
                m_safeArea = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public byte Sound
        {
            get
            {
                return m_sound;
            }
            set
            {
                Dirty = true;
                m_sound = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool CheckLOS
        {
            get
            {
                return m_checkLOS;
            }
            set
            {
                Dirty = true;
                m_checkLOS = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string Points
        {
            get
            {
                return m_points;
            }
            set
            {
                Dirty = true;
                m_points = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int RealmPoints
        {
            get
            {
                return this.realmPoints;
            }

            set
            {
                this.Dirty = true;
                this.realmPoints = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool IsPvP
        {
            get
            {
                return m_isPvp;
            }

            set
            {
                Dirty = true;
                m_isPvp = value;
            }
        }
    }
}