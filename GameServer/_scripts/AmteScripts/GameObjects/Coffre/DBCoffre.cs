/* Maj des tables:
ALTER TABLE `coffre` ADD `KeyItem` TEXT NOT NULL ,
ADD `LockDifficult` INT( 11 ) NOT NULL ;
*/

using System;
using DOL.GS;
using DOL.GS.Scripts;
using DOL.Events;
using DOL.Database.Attributes;
using System.Reflection;
using log4net;
using DOL.GameEvents;
using System.Linq;
using System.Collections.Generic;

namespace DOL.Database
{
    /// <summary>
    /// Coffre
    /// </summary>
    [DataTable(TableName = "Coffre")]
    public class DBCoffre : DataObject
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        [ScriptLoadedEvent]
        public static void OnScriptsCompiled(DOLEvent e, object sender, EventArgs args)
        {
            GameServer.Database.RegisterDataObject(typeof(DBCoffre));
            log.Info("DATABASE Coffre LOADED");
        }

        [GameServerStartedEvent]
        public static void OnServerStarted(DOLEvent e, object sender, EventArgs args)
        {
            int i = 0;
            foreach (var obj in GameServer.Database.SelectAllObjects<DBCoffre>())
            {
                i++;
                GameCoffre coffre = new GameCoffre();

                if (obj.Name.StartsWith("Catapult_Backbone"))
                {
                    coffre = new GameStaticCatapult();
                }
                else
                {
                    coffre = new GameCoffre();
                }

                coffre.LoadFromDatabase(obj);

                if (obj.EventID == null)
                {
                    coffre.AddToWorld();
                }
                else
                {
                    GameEventManager.Instance.PreloadedCoffres.Add(coffre);
                }
            }
            log.Info(i + " GameCoffre loaded in world.");
            GameEventMgr.Notify(GameServerEvent.CoffreLoaded);
        }


        private string m_name;
        private int m_x;
        private int m_y;
        private int m_z;
        private ushort m_heading;
        private ushort m_region;
        private ushort m_model;
        private int m_itemInterval;
        private DateTime m_lastOpen;
        private string m_itemList;
        private int m_itemChance;
        private int m_lockDifficult;
        private string m_keyItem;
        private int m_trapRate;
        private string m_npctemplateId;
        private int m_tpY;
        private int m_tpX;
        private int m_tpZ;
        private bool m_isTeleporter;
        private int m_tpLevelRequirement;
        private bool m_tpIsRenaissance;
        private int m_tpEffect;
        private int m_tpRegion;
        private bool m_isOpeningRenaissanceType;
        private int m_punishSpellId;
        private int m_tpHeading;
        private bool m_hasPickableAnim;
        private int m_coffreOpeningInterval;
        private string m_eventID;
        private List<string> m_removedByEventID;
        private int m_tpid;
        private bool m_shouldrespawntotpid;
        protected int m_currentStep;
        private bool m_pickontouch;
        private int m_keyLoseDur;
        private string m_switchFamily;
        private int m_switchOrder;
        private bool m_isSwitch;
        private bool m_wrongorderresetfamily;
        private int m_secondarymodel;
        private int m_activatedDuration;
        private string m_activatedbyswitchon;
        private string m_activatedbyswitchoff;
        private string m_resetbyswitchon;
        private string m_resetbyswitchoff;
        private int m_switchonsound;
        private int m_wrongfamilyordersound;
        private int m_activatedfamilysound;
        private int m_deactivatedfamilysound;
        private string m_lootGenerator;

        [DataElement(AllowDbNull = false)]
        public string Name
        {
            get
            {
                return m_name;
            }
            set
            {
                Dirty = true;
                m_name = value;
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
        public int TPHeading
        {
            get
            {
                return m_tpHeading;
            }
            set
            {
                Dirty = true;
                m_tpHeading = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public ushort Heading
        {
            get
            {
                return m_heading;
            }
            set
            {
                Dirty = true;
                m_heading = value;
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

        [DataElement(AllowDbNull = false)]
        public ushort Model
        {
            get
            {
                return m_model;
            }
            set
            {
                Dirty = true;
                m_model = value;
            }
        }


        [DataElement(AllowDbNull = false)]
        public int CoffreOpeningInterval
        {
            get
            {
                return m_coffreOpeningInterval;
            }

            set
            {
                m_coffreOpeningInterval = value;
                Dirty = true;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool IsLargeCoffre
        {
            get;
            set;
        }

        /// <summary>
        /// Temps en minutes avant la réapparition d'un item
        /// </summary>
        [DataElement(AllowDbNull = false)]
        public int ItemInterval
        {
            get
            {
                return m_itemInterval;
            }
            set
            {
                Dirty = true;
                m_itemInterval = value;
            }
        }

        /// <summary>
        /// Pourcentage de chance d'avoir un item
        /// </summary>
        [DataElement(AllowDbNull = false)]
        public int ItemChance
        {
            get
            {
                return m_itemChance;
            }
            set
            {
                Dirty = true;
                m_itemChance = value;
            }
        }

        /// <summary>
        /// Pourcentage de chance de voir pop un mob
        /// </summary>
        [DataElement(AllowDbNull = true)]
        public int TrapRate
        {
            get
            {
                return m_trapRate;
            }
            set
            {
                Dirty = true;
                m_trapRate = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string NpctemplateId
        {
            get
            {
                return m_npctemplateId;
            }

            set
            {
                Dirty = true;
                m_npctemplateId = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public DateTime LastOpen
        {
            get
            {
                return m_lastOpen;
            }
            set
            {
                Dirty = true;
                m_lastOpen = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public string ItemList
        {
            get
            {
                return m_itemList;
            }
            set
            {
                Dirty = true;
                m_itemList = value;
            }
        }

        /// <summary>
        /// Difficulté d'ouvrir la serrure avec un crochet (sur 1000)
        /// </summary>
        [DataElement(AllowDbNull = false)]
        public int LockDifficult
        {
            get
            {
                return m_lockDifficult;
            }
            set
            {
                Dirty = true;
                m_lockDifficult = value;
            }
        }

        /// <summary>
        /// Id_nb de la clef pour ouvrir le coffre
        /// </summary>
        [DataElement(AllowDbNull = false)]
        public string KeyItem
        {
            get
            {
                return m_keyItem;
            }
            set
            {
                Dirty = true;
                m_keyItem = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int TpX
        {
            get
            {
                return m_tpX;
            }
            set
            {
                Dirty = true;
                m_tpX = value;
            }
        }
        [DataElement(AllowDbNull = true)]
        public int TpY
        {
            get
            {
                return m_tpY;
            }
            set
            {
                Dirty = true;
                m_tpY = value;
            }
        }
        [DataElement(AllowDbNull = true)]
        public int TpZ
        {
            get
            {
                return m_tpZ;
            }
            set
            {
                Dirty = true;
                m_tpZ = value;
            }
        }
        [DataElement(AllowDbNull = true)]
        public bool IsTeleporter
        {
            get
            {
                return m_isTeleporter;
            }
            set
            {
                Dirty = true;
                m_isTeleporter = value;
            }
        }
        [DataElement(AllowDbNull = true)]
        public int TpLevelRequirement
        {
            get
            {
                return m_tpLevelRequirement;
            }
            set
            {
                Dirty = true;
                m_tpLevelRequirement = value;
            }
        }
        [DataElement(AllowDbNull = true)]
        public bool TpIsRenaissance
        {
            get
            {
                return m_tpIsRenaissance;
            }
            set
            {
                Dirty = true;
                m_tpIsRenaissance = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool HasPickableAnim
        {
            get
            {
                return m_hasPickableAnim;
            }

            set
            {
                Dirty = true;
                m_hasPickableAnim = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int PunishSpellId
        {
            get
            {
                return m_punishSpellId;
            }

            set
            {
                Dirty = true;
                m_punishSpellId = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public bool IsOpeningRenaissanceType
        {
            get
            {
                return m_isOpeningRenaissanceType;
            }

            set
            {
                Dirty = true;
                m_isOpeningRenaissanceType = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int TpEffect
        {
            get
            {
                return m_tpEffect;
            }
            set
            {
                Dirty = true;
                m_tpEffect = value;
            }
        }
        [DataElement(AllowDbNull = true)]
        public int TpRegion
        {
            get
            {
                return m_tpRegion;
            }
            set
            {
                Dirty = true;
                m_tpRegion = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string EventID
        {
            get
            {
                return m_eventID;
            }

            set
            {
                Dirty = true;
                m_eventID = value;
            }
        }
        /// <summary>
        /// List of events removing this
        /// </summary>
        [DataElement(AllowDbNull = true)]
        public string RemovedByEventID
        {
            get {
                if (m_removedByEventID != null)
                    return string.Join("|", m_removedByEventID);
                else 
                    return "";
            }
            set
            {
                if(value!= null)
                    m_removedByEventID = value.Split('|').ToList();
                Dirty = true;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int TPID
        {
            get
            {
                return m_tpid;
            }
            set
            {
                Dirty = true;
                m_tpid = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool ShouldRespawnToTPID
        {
            get
            {
                return m_shouldrespawntotpid;
            }

            set
            {
                Dirty = true;
                m_shouldrespawntotpid = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int CurrentStep
        {
            get { return m_currentStep; }
            set
            {
                Dirty = true;
                m_currentStep = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool PickOnTouch
        {
            get
            {
                return m_pickontouch;
            }

            set
            {
                Dirty = true;
                m_pickontouch = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool IsOpenableOnce { get; set; }

        [DataElement(AllowDbNull = false)]
        public bool IsTerritoryLinked { get; set; }

        [DataElement(AllowDbNull = true)]
        public int KeyLoseDur
        {
            get
            {
                return m_keyLoseDur;
            }
            set
            {
                Dirty = true;
                m_keyLoseDur = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public string SwitchFamily
        {
            get
            {
                return m_switchFamily;
            }
            set
            {
                Dirty = true;
                m_switchFamily = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int SwitchOrder
        {
            get
            {
                return m_switchOrder;
            }
            set
            {
                Dirty = true;
                m_switchOrder = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public bool IsSwitch
        {
            get
            {
                return m_isSwitch;
            }
            set
            {
                Dirty = true;
                m_isSwitch = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public bool WrongOrderResetFamily
        {
            get
            {
                return m_wrongorderresetfamily;
            }

            set
            {
                Dirty = true;
                m_wrongorderresetfamily = value;
            }
        }

        [DataElement(AllowDbNull = false)]
        public int SecondaryModel
        {
            get
            {
                return m_secondarymodel;
            }
            set
            {
                Dirty = true;
                m_secondarymodel = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int ActivatedDuration
        {
            get { return m_activatedDuration; }
            set
            {
                Dirty = true;
                m_activatedDuration = value;
            }
        }

        [DataElement(AllowDbNull = true, Varchar = 255)]
        public string ActivatedBySwitchOn
        {
            get => m_activatedbyswitchon;
            set
            {
                Dirty = true;
                m_activatedbyswitchon = value;
            }
        }

        [DataElement(AllowDbNull = true, Varchar = 255)]
        public string ActivatedBySwitchOff
        {
            get => m_activatedbyswitchoff;
            set
            {
                Dirty = true;
                m_activatedbyswitchoff = value;
            }
        }

        [DataElement(AllowDbNull = true, Varchar = 255)]
        public string ResetBySwitchOn
        {
            get => m_resetbyswitchon;
            set
            {
                Dirty = true;
                m_resetbyswitchon = value;
            }
        }

        [DataElement(AllowDbNull = true, Varchar = 255)]
        public string ResetBySwitchOff
        {
            get => m_resetbyswitchoff;
            set
            {
                Dirty = true;
                m_resetbyswitchoff = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int SwitchOnSound
        {
            get { return m_switchonsound; }
            set
            {
                Dirty = true;
                m_switchonsound = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int WrongFamilyOrderSound
        {
            get { return m_wrongfamilyordersound; }
            set
            {
                Dirty = true;
                m_wrongfamilyordersound = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int ActivatedFamilySound
        {
            get { return m_activatedfamilysound; }
            set
            {
                Dirty = true;
                m_activatedfamilysound = value;
            }
        }

        [DataElement(AllowDbNull = true)]
        public int DeactivatedFamilySound
        {
            get { return m_deactivatedfamilysound; }
            set
            {
                Dirty = true;
                m_deactivatedfamilysound = value;
            }
        }

        [DataElement(AllowDbNull = false, Varchar = 255)]
        public string LootGenerator
        {
            get
            {
                return m_lootGenerator;
            }
            set
            {
                Dirty = true;
                m_lootGenerator = value;
            }
        }
    }
}
