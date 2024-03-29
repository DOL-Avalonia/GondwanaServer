using System;
using System.Reflection;
using DOL.GS;
using DOL.Events;
using DOL.Database.Attributes;
using log4net;

namespace DOL.Database
{
    /// <summary>
    /// TradingPNJ
    /// </summary>
    [DataTable(TableName = "Echangeur")]
    public class DBEchangeur : DataObject
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private string m_npcID;
        private string m_itemRecvID;
        private int m_itemRecvCount;
        private string m_itemGiveID;
        private int m_itemGiveCount;
        private long m_gainMoney;
        private int m_gainXP;
        private int m_changedItemCount;

        private ItemTemplate m_GiveTemplate;
        private long m_moneyPrice;
        private string m_priceRessource1;
        private string m_priceRessource2;
        private string m_priceRessource3;

        private string m_queatName;
        private int m_step;

        public ItemTemplate GiveTemplate
        {
            get
            {
                if (m_GiveTemplate == null)
                {
                    if (String.IsNullOrEmpty(m_itemGiveID))
                        return null;
                    m_GiveTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(m_itemGiveID);
                }
                return m_GiveTemplate;
            }
        }

        [GameServerStartedEvent]
        public static void OnScriptsCompiled(DOLEvent e, object sender, EventArgs args)
        {
            GameServer.Database.RegisterDataObject(typeof(DBEchangeur));
            log.Info("DATABASE Echangeur LOADED");
        }

        [DataElement(AllowDbNull = true)]
        public string NpcID
        {
            get
            {
                return m_npcID;
            }
            set
            {
                Dirty = true;
                m_npcID = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string ItemRecvID
        {
            get
            {
                return m_itemRecvID;
            }
            set
            {
                Dirty = true;
                m_itemRecvID = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int ItemRecvCount
        {
            get
            {
                return m_itemRecvCount;
            }
            set
            {
                Dirty = true;
                m_itemRecvCount = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string ItemGiveID
        {
            get
            {
                return m_itemGiveID;
            }
            set
            {
                Dirty = true;
                m_itemGiveID = value;
                m_GiveTemplate = null;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int ItemGiveCount
        {
            get
            {
                return m_itemGiveCount;
            }
            set
            {
                Dirty = true;
                m_itemGiveCount = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public long GainMoney
        {
            get
            {
                return m_gainMoney;
            }
            set
            {
                Dirty = true;
                m_gainMoney = value;
            }
        }


        [DataElement(AllowDbNull = false)]
        public int GainXP
        {
            get
            {
                return m_gainXP;
            }
            set
            {
                Dirty = true;
                m_gainXP = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int ChangedItemCount
        {
            get { return m_changedItemCount; }
            set
            {
                Dirty = true;
                m_changedItemCount = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public long MoneyPrice
        {
            get
            {
                return m_moneyPrice;
            }

            set
            {
                m_moneyPrice = value;
                Dirty = true;
            }
        }

        [DataElement(AllowDbNull = true, Varchar = 255)]
        public string PriceRessource1
        {
            get
            {
                return m_priceRessource1;
            }

            set
            {
                m_priceRessource1 = value;
                Dirty = true;
            }
        }

        [DataElement(AllowDbNull = true, Varchar = 255)]
        public string PriceRessource2
        {
            get
            {
                return m_priceRessource2;
            }

            set
            {
                m_priceRessource2 = value;
                Dirty = true;
            }
        }

        [DataElement(AllowDbNull = true, Varchar = 255)]
        public string PriceRessource3
        {
            get
            {
                return m_priceRessource3;
            }

            set
            {
                m_priceRessource3 = value;
                Dirty = true;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string QuestName
        {
            get
            {
                return m_queatName;
            }

            set
            {
                m_queatName = value;
                Dirty = true;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int Step
        {
            get
            {
                return m_step;
            }

            set
            {
                m_step = value;
                Dirty = true;
            }
        }
    }
}
