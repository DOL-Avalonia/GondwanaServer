using System;
using DOL.Database.Attributes;

namespace DOL.Database
{
    [DataTable(TableName = "DataQuestJsonCooldown")]
    public class DBDataQuestJsonCooldown : DataObject
    {
        private string m_playerId;
        private ushort m_questId;
        private DateTime m_completedUtc;
        private DateTime m_nextAvailableUtc;

        [PrimaryKey]
        public string PlayerID
        {
            get => m_playerId;
            set { m_playerId = value; Dirty = true; }
        }

        [PrimaryKey]
        public ushort QuestID
        {
            get => m_questId;
            set { m_questId = value; Dirty = true; }
        }

        [DataElement(AllowDbNull = false)]
        public DateTime CompletedUtc
        {
            get => m_completedUtc;
            set { m_completedUtc = value; Dirty = true; }
        }

        [DataElement(AllowDbNull = false)]
        public DateTime NextAvailableUtc
        {
            get => m_nextAvailableUtc;
            set { m_nextAvailableUtc = value; Dirty = true; }
        }
    }
}
