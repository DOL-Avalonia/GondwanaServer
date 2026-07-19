using System.Collections.Generic;
using System.Reflection;
using System;
using DOL.Database;
using DOL.GS.Keeps;
using DOL.GS.Geometry;
using log4net;

namespace DOL.GS
{
    /// <summary>
    /// DoorMgr is manager of all door regular door and keep door
    /// </summary>
    public sealed class DoorMgr
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private static readonly object Lock = new object();
        private static Dictionary<int, IDoor> m_doors = new Dictionary<int, IDoor>();

        public const string WANT_TO_ADD_DOORS = "WantToAddDoors";

        /// <summary>
        /// this function load all door from DB
        /// </summary>
        public static bool Init()
        {
            var dbdoors = GameServer.Database.SelectAllObjects<DBDoor>();
            foreach (DBDoor door in dbdoors)
            {
                int type = door.Type > 0 ? door.Type : door.InternalID / 100000000;

                if (type == 7)
                    continue;

                if (!LoadDoor(door))
                {
                    log.Error("Unable to load door id " + door.ObjectId + ", correct your database");
                }
            }
            return true;
        }

        public static bool LoadDoor(DBDoor door)
        {
            IDoor mydoor = null;
            ushort zone = (ushort)(door.InternalID / 1000000);

            Zone currentZone = WorldMgr.GetZone(zone);
            if (currentZone == null) return false;

            foreach (AbstractArea area in currentZone.GetAreasOfSpot(Coordinate.Create(door.X, door.Y, door.Z)))
            {
                if (area is KeepArea)
                {
                    mydoor = new GameKeepDoor();
                    mydoor.LoadFromDatabase(door);
                    break;
                }
            }

            if (mydoor == null)
            {
                mydoor = new GameDoor();
                mydoor.LoadFromDatabase(door);
            }

            if (mydoor != null)
                RegisterDoor(mydoor);

            return true;
        }

        public static void RegisterDoor(IDoor door)
        {
            lock (Lock)
            {
                if (m_doors.TryGetValue(door.DoorID, out IDoor existingDoor))
                {
                    if (existingDoor == door) return;

                    if (log.IsDebugEnabled)
                        log.Debug($"Door ID {door.DoorID} is already registered. Overwriting.");
                }

                m_doors[door.DoorID] = door;

                if (PathingMgr.Instance != null && door.ZoneID != 0)
                {
                    if (!PathingMgr.Instance.RegisterDoor(door))
                    {
                        if (log.IsDebugEnabled)
                            log.Debug($"Failed to register door in Navmesh. (Id: {door.DoorID} Name: {door.Name})");
                    }
                }
            }
        }

        public static void UnRegisterDoor(int doorID)
        {
            lock (Lock)
            {
                m_doors.Remove(doorID);
            }
        }

        public static IDoor GetDoorByID(int id)
        {
            lock (Lock)
            {
                return m_doors.TryGetValue(id, out IDoor door) ? door : null;
            }
        }

        /// <summary>
        /// This function get the door object by door index: DoorRequestHandler expects a List<IDoor>. 
        /// </summary>
        [Obsolete("Use GetDoorByID going forward.")]
        public static List<IDoor> getDoorByID(int id)
        {
            var door = GetDoorByID(id);
            return door != null ? new List<IDoor> { door } : new List<IDoor>();
        }

        public static List<GameDoor> GetDoorsBySwitchFamily(string switchFamily)
        {
            List<GameDoor> doors = new List<GameDoor>();
            lock (Lock)
            {
                foreach (var door in m_doors.Values)
                {
                    if (door is GameDoor gameDoor && gameDoor.SwitchFamily == switchFamily)
                        doors.Add(gameDoor);
                }
            }
            return doors;
        }

        public static void UnlockDoorsBySwitchFamily(string switchFamily)
        {
            var doorsToUnlock = GetDoorsBySwitchFamily(switchFamily);
            foreach (var gameDoor in doorsToUnlock)
            {
                gameDoor.UnlockBySwitch();
            }
        }
    }
}