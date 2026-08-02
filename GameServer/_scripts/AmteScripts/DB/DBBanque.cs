using System;
using DOL.Database;
using DOL.Database.Attributes;
using DOL.Events;
using DOL.GS;

namespace DOL.Database
{
    [DataTable(TableName = "Banque")]
    public class DBBanque : DataObject
    {
        private string m_PlayerID;
        private long m_Money;
        private bool m_AutoPayRent;
        private long m_LoanAmount;
        private long m_LoanOriginalAmount;
        private int m_LoanDaysRemaining;
        private int m_LoanType; // 0=none, 1=personal, 2=house
        private DateTime m_LastLoanPayment = DateTime.MinValue;
        private DateTime m_NegativeMoneySince = DateTime.MinValue;
        private DateTime m_LoanRestrictionUntil = DateTime.MinValue;
        private bool m_IsDebtor;
        private long m_Debt;

        [PrimaryKey]
        public string PlayerID
        {
            get { return m_PlayerID; }
            set { m_PlayerID = value; }
        }

        [DataElement(AllowDbNull = false)]
        public long Money
        {
            get { return m_Money; }
            set { Dirty = true; m_Money = value; }
        }

        [DataElement(AllowDbNull = false)]
        public bool AutoPayRent
        {
            get { return m_AutoPayRent; }
            set { Dirty = true; m_AutoPayRent = value; }
        }

        [DataElement(AllowDbNull = true)]
        public long LoanAmount
        {
            get { return m_LoanAmount; }
            set { Dirty = true; m_LoanAmount = value; }
        }

        [DataElement(AllowDbNull = true)]
        public long LoanOriginalAmount
        {
            get { return m_LoanOriginalAmount; }
            set { Dirty = true; m_LoanOriginalAmount = value; }
        }

        [DataElement(AllowDbNull = true)]
        public int LoanDaysRemaining
        {
            get { return m_LoanDaysRemaining; }
            set { Dirty = true; m_LoanDaysRemaining = value; }
        }

        [DataElement(AllowDbNull = true)]
        public int LoanType
        {
            get { return m_LoanType; }
            set { Dirty = true; m_LoanType = value; }
        }

        [DataElement(AllowDbNull = true)]
        public DateTime LastLoanPayment
        {
            get { return m_LastLoanPayment; }
            set { Dirty = true; m_LastLoanPayment = value; }
        }

        [DataElement(AllowDbNull = true)]
        public DateTime NegativeMoneySince
        {
            get { return m_NegativeMoneySince; }
            set { Dirty = true; m_NegativeMoneySince = value; }
        }

        [DataElement(AllowDbNull = true)]
        public DateTime LoanRestrictionUntil
        {
            get { return m_LoanRestrictionUntil; }
            set { Dirty = true; m_LoanRestrictionUntil = value; }
        }

        [DataElement(AllowDbNull = true)]
        public bool IsDebtor
        {
            get { return m_IsDebtor; }
            set { Dirty = true; m_IsDebtor = value; }
        }

        [DataElement(AllowDbNull = true)]
        public long Debt
        {
            get { return m_Debt; }
            set { Dirty = true; m_Debt = value; }
        }

        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            GameServer.Database.RegisterDataObject(typeof(DBBanque));
            GameServer.Database.RegisterDataObject(typeof(DBTermDeposit));
        }

        public DBBanque()
        {
            m_PlayerID = null;
            m_Money = 0;
            m_AutoPayRent = false;
        }

        public DBBanque(string playerID)
        {
            m_PlayerID = playerID;
            m_Money = 0;
            m_AutoPayRent = false;
        }
    }
}
