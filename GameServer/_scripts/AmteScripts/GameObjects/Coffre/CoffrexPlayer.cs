
using System;
using DOL.GS;
using DOL.Events;
using DOL.Database.Attributes;
using DOL.Database;
using System.Reflection;
using log4net;

namespace DOL.Database
{
    /// <summary>
    /// CoffrexPlayer
    /// </summary>
    [DataTable(TableName = "CoffrexPlayer")]
    public class CoffrexPlayer : DataObject
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        [ScriptLoadedEvent]
        public static void OnScriptsCompiled(DOLEvent e, object sender, EventArgs args)
        {
            GameServer.Database.RegisterDataObject(typeof(CoffrexPlayer));
            log.Info("Database CoffrexPlayer table loaded.");
        }

        private string m_playerid;
        private string m_coffreid;
        private DateTime m_lasttimerowupdated;

        [DataElement(AllowDbNull = false)]
        public string PlayerID
        {
            get
            {
                return m_playerid;
            }
            set
            {
                Dirty = true;
                m_playerid = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public string CoffreID
        {
            get
            {
                return m_coffreid;
            }
            set
            {
                Dirty = true;
                m_coffreid = value;
            }
        }

        public CoffrexPlayer()
        {
        }
    }
}
