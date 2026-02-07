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
        private int m_maxRadius;
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
        private int m_boundary = 0;
        private int m_boundaryEvent = 0;
        private int m_boundarySpacing = 0;
        private int m_spellID = 0;
        private int m_spellIDEvent = 0;
        private int m_effectAmount = 0;
        private int m_effectFrequency = 0;
        private byte m_stormLevel = 25;
        private byte m_stormSize = 60;
        private double m_effectVariance;
        private string m_eventList;
        private bool m_isRadioactive;

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
        public int MaxRadius
        {
            get { return m_maxRadius; }
            set
            {
                Dirty = true;
                m_maxRadius = value;
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

        [DataElement(AllowDbNull = false)]
        public int Boundary
        {
            get { return m_boundary; }
            set { Dirty = true; m_boundary = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int BoundaryEvent
        {
            get { return m_boundaryEvent; }
            set { Dirty = true; m_boundaryEvent = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int BoundarySpacing
        {
            get => m_boundarySpacing;
            set { Dirty = true; m_boundarySpacing = value; }
        }

        [DataElement(AllowDbNull = true)]
        public int SpellID
        {
            get => m_spellID;
            set { Dirty = true; m_spellID = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int SpellIDEvent
        {
            get => m_spellIDEvent;
            set { Dirty = true; m_spellIDEvent = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int EffectAmount
        {
            get => m_effectAmount;
            set { Dirty = true; m_effectAmount = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int EffectFrequency
        {
            get => m_effectFrequency;
            set { Dirty = true; m_effectFrequency = value; }
        }

        [DataElement(AllowDbNull = false)]
        public byte StormLevel
        {
            get => m_stormLevel;
            set { Dirty = true; m_stormLevel = value; }
        }

        [DataElement(AllowDbNull = false)]
        public byte StormSize
        {
            get => m_stormSize;
            set { Dirty = true; m_stormSize = value; }
        }

        [DataElement(AllowDbNull = false)]
        public double EffectVariance
        {
            get => m_effectVariance;
            set { Dirty = true; m_effectVariance = value; }
        }

        [DataElement(AllowDbNull = true)]
        public string EventList
        {
            get => m_eventList;
            set { Dirty = true; m_eventList = value; }
        }

        [DataElement(AllowDbNull = false)]
        public bool IsRadioactive
        {
            get { return m_isRadioactive; }
            set
            {
                Dirty = true;
                m_isRadioactive = value;
            }
        }
    }
}