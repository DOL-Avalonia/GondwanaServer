using DOL.Database;
using DOL.Database.Attributes;

namespace DOLDatabase.Tables
{
    [DataTable(TableName = "bossparameters")]
    public class DBBossParameters : DataObject
    {
        private string m_bossID;
        private int m_spellmagicABS;
        private int m_dotABS;
        private int m_meleeABS;
        private int m_maxhealth;
        private int m_effectiveness;
        private int m_bossDifficulty;
        private double m_ablativeShield;
        private int m_erodibleAblative;
        private double m_playerInRadiusEnhancement;

        public DBBossParameters()
        {
            m_spellmagicABS = 0;
            m_dotABS = 0;
            m_meleeABS = 0;
            m_maxhealth = 0;
            m_effectiveness = 0;
            m_bossDifficulty = 0;
            m_ablativeShield = 0.0;
            m_erodibleAblative = 1;
        }

        [DataElement(AllowDbNull = false, Index = true)]
        public string BossID
        {
            get { return m_bossID; }
            set { Dirty = true; m_bossID = value; }
        }

        [DataElement(AllowDbNull = true)]
        public int SpellmagicABS
        {
            get { return m_spellmagicABS; }
            set { Dirty = true; m_spellmagicABS = value; }
        }

        [DataElement(AllowDbNull = true)]
        public int DotABS
        {
            get { return m_dotABS; }
            set { Dirty = true; m_dotABS = value; }
        }

        [DataElement(AllowDbNull = true)]
        public int MeleeABS
        {
            get { return m_meleeABS; }
            set { Dirty = true; m_meleeABS = value; }
        }

        [DataElement(AllowDbNull = true)]
        public int Maxhealth
        {
            get { return m_maxhealth; }
            set { Dirty = true; m_maxhealth = value; }
        }

        [DataElement(AllowDbNull = true)]
        public int Effectiveness
        {
            get { return m_effectiveness; }
            set { Dirty = true; m_effectiveness = value; }
        }

        [DataElement(AllowDbNull = true)]
        public int BossDifficulty
        {
            get { return m_bossDifficulty; }
            set { Dirty = true; m_bossDifficulty = value; }
        }

        [DataElement(AllowDbNull = true)]
        public double AblativeShield
        {
            get { return m_ablativeShield; }
            set { Dirty = true; m_ablativeShield = value; }
        }

        [DataElement(AllowDbNull = true)]
        public int ErodibleAblative
        {
            get { return m_erodibleAblative; }
            set { Dirty = true; m_erodibleAblative = value; }
        }

        [DataElement(AllowDbNull = true)]
        public double PlayerInRadiusEnhancement
        {
            get { return m_playerInRadiusEnhancement; }
            set { Dirty = true; m_playerInRadiusEnhancement = value; }
        }
    }
}