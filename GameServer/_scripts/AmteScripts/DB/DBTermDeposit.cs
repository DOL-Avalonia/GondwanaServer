using System;
using DOL.Database;
using DOL.Database.Attributes;

namespace DOL.Database
{
    [DataTable(TableName = "TermDeposits")]
    public class DBTermDeposit : DataObject
    {
        private string m_PlayerID;
        private long m_Amount;
        private DateTime m_MaturityDate;
        private int m_InterestRate;

        [DataElement(AllowDbNull = false)]
        public string PlayerID
        {
            get { return m_PlayerID; }
            set { Dirty = true; m_PlayerID = value; }
        }

        [DataElement(AllowDbNull = false)]
        public long Amount
        {
            get { return m_Amount; }
            set { Dirty = true; m_Amount = value; }
        }

        [DataElement(AllowDbNull = false)]
        public DateTime MaturityDate
        {
            get { return m_MaturityDate; }
            set { Dirty = true; m_MaturityDate = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int InterestRate
        {
            get { return m_InterestRate; }
            set { Dirty = true; m_InterestRate = value; }
        }

        public DBTermDeposit() { }
    }
}