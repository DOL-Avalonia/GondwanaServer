using System;
using DOL.Database;
using DOL.Database.Attributes;

namespace DOL.Database
{
    /// <summary>
    /// Represents a single PvP Session configuration
    /// stored in table "PvPManager".
    /// Columns: SessionID, ZoneList, SessionType, Frequency, ...
    /// </summary>
    [DataTable(TableName = "PvPManager")]
    public class PvpSession : DataObject
    {
        private string _sessionID;
        private string _zoneList;
        private int _sessionType;
        private int _frequency;
        private int _groupCompoOption;
        private string _spawnOption; // "RandomLock","RandomUnlock", or maybe numeric
        private int _groupMaxSize;
        private bool _allowGroupDisbandCreate;
        private bool _allowSummonBanner;
        private bool _createCustomArea;
        private int _tempAreaRadius;
        private int _minPlayersForScore;

        [PrimaryKey] // we assume SessionID is unique
        public string SessionID
        {
            get { return _sessionID; }
            set { Dirty = true; _sessionID = value; }
        }

        [DataElement(AllowDbNull = false)]
        public string ZoneList
        {
            get { return _zoneList; }
            set { Dirty = true; _zoneList = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int SessionType
        {
            get { return _sessionType; }
            set { Dirty = true; _sessionType = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int Frequency
        {
            get { return _frequency; }
            set { Dirty = true; _frequency = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int GroupCompoOption
        {
            get { return _groupCompoOption; }
            set { Dirty = true; _groupCompoOption = value; }
        }

        [DataElement(AllowDbNull = false)]
        public string SpawnOption
        {
            get { return _spawnOption; }
            set { Dirty = true; _spawnOption = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int GroupMaxSize
        {
            get { return _groupMaxSize; }
            set { Dirty = true; _groupMaxSize = value; }
        }

        [DataElement(AllowDbNull = false)]
        public bool AllowGroupDisbandCreate
        {
            get { return _allowGroupDisbandCreate; }
            set { Dirty = true; _allowGroupDisbandCreate = value; }
        }

        [DataElement(AllowDbNull = false)]
        public bool AllowSummonBanner
        {
            get { return _allowSummonBanner; }
            set { Dirty = true; _allowSummonBanner = value; }
        }

        [DataElement(AllowDbNull = false)]
        public bool CreateCustomArea
        {
            get { return _createCustomArea; }
            set { Dirty = true; _createCustomArea = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int TempAreaRadius
        {
            get { return _tempAreaRadius; }
            set { Dirty = true; _tempAreaRadius = value; }
        }

        [DataElement(AllowDbNull = false)]
        public int MinPlayersForScore
        {
            get { return _minPlayersForScore; }
            set { Dirty = true; _minPlayersForScore = value; }
        }
    }
}