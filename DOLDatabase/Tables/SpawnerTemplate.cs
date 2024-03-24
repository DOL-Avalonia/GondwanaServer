using DOL.Database;
using DOL.Database.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOLDatabase.Tables
{
    [DataTable(TableName = "SpawnerTemplate")]
    public class SpawnerTemplate
        : DataObject
    {
        private string m_mobID;
        private int m_npcTemplate1;
        private int m_npcTemplate2;
        private int m_npcTemplate3;
        private int m_npcTemplate4;
        private int m_npcTemplate5;
        private int m_npcTemplate6;
        private bool m_isAggroType;
        private int m_percentLifeAddsActivity;
        private string m_inactiveStatusId;
        private string m_activeStatusId;
        private int m_addsRespawnCount;
        private string m_masterGroupId;
        private int m_addRespawnTimerSecs;
        private int m_percentageOfPlayerInRadius;

        [DataElement(AllowDbNull = false, Varchar = 255, Index = true)]
        public string MobID
        {
            get => m_mobID;
            set { Dirty = true; m_mobID = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int NpcTemplate1
        {
            get => m_npcTemplate1;
            set { Dirty = true; m_npcTemplate1 = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int NpcTemplate2
        {
            get => m_npcTemplate2;
            set { Dirty = true; m_npcTemplate2 = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int NpcTemplate3
        {
            get => m_npcTemplate3;
            set { Dirty = true; m_npcTemplate3 = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int NpcTemplate4
        {
            get => m_npcTemplate4;
            set { Dirty = true; m_npcTemplate4 = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int NpcTemplate5
        {
            get => m_npcTemplate5;
            set { Dirty = true; m_npcTemplate5 = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int NpcTemplate6
        {
            get => m_npcTemplate6;
            set { Dirty = true; m_npcTemplate6 = value; }
        }

        [DataElement(AllowDbNull = true, Varchar = 255)]
        public string MasterGroupId
        {
            get => m_masterGroupId;
            set { Dirty = true; m_masterGroupId = value; }
        }

        [DataElement(AllowDbNull = true)]
        public int PercentageOfPlayerInRadius
        {
            get { return m_percentageOfPlayerInRadius; }
            set
            {
                Dirty = true;
                m_percentageOfPlayerInRadius = value;
            }
        }

        [DataElement(AllowDbNull = true, Varchar = 255)]
        public string InactiveStatusId
        {
            get => m_inactiveStatusId;
            set { Dirty = true; m_inactiveStatusId = value; }
        }

        [DataElement(AllowDbNull = true, Varchar = 255)]
        public string ActiveStatusId
        {
            get => m_activeStatusId;
            set { Dirty = true; m_activeStatusId = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int AddsRespawnCount
        {
            get => m_addsRespawnCount;
            set { Dirty = true; m_addsRespawnCount = value; }
        }

        [DataElement(AllowDbNull = false)]
        public bool IsAggroType
        {
            get => m_isAggroType;
            set { Dirty = true; m_isAggroType = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int PercentLifeAddsActivity
        {
            get => m_percentLifeAddsActivity;
            set { Dirty = true; m_percentLifeAddsActivity = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int AddRespawnTimerSecs
        {
            get => m_addRespawnTimerSecs;
            set { Dirty = true; m_addRespawnTimerSecs = value; }
        }
    }
}