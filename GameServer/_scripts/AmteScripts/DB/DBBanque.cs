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
