using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using DOL.Database;
using DOL.Language;
using DOL.GS.Geometry;
using log4net;

namespace DOL.GS
{
    public class Zone : ITranslatableObject
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private const ushort SUBZONE_NBR_ON_ZONE_SIDE = 32;
        private const ushort SUBZONE_NBR = (ushort)(SUBZONE_NBR_ON_ZONE_SIDE * SUBZONE_NBR_ON_ZONE_SIDE);
        private const ushort SUBZONE_SIZE = (ushort)(65536 / SUBZONE_NBR_ON_ZONE_SIDE);
        private static readonly ushort SUBZONE_SHIFT = (ushort)Math.Round(Math.Log(SUBZONE_SIZE) / Math.Log(2));
        private static readonly ushort SUBZONE_ARRAY_Y_SHIFT = (ushort)Math.Round(Math.Log(SUBZONE_NBR_ON_ZONE_SIDE) / Math.Log(2));

        public const ushort MAX_REFRESH_INTERVAL = 2000;

        public enum eGameObjectType : byte { ITEM = 0, NPC = 1, PLAYER = 2, DOOR = 3, }

        private readonly SubZone[] _subZones = new SubZone[SUBZONE_NBR];
        private int _objectCount;

        private bool m_allowReputation;
        private bool m_isDungeon;
        private Region m_Region;
        private readonly ushort m_ID;
        private ushort m_zoneSkinID;
        private string m_Description;
        public Vector Offset { get; init; }
        private readonly int m_Width;
        private readonly int m_Height;
        public int XOffset => Offset.X;
        public int YOffset => Offset.Y;
        public int ZOffset => Offset.Z;
        private int m_waterlevel;
        private bool m_isDivingEnabled;
        private int m_maxFlyAltitude = 10000;
        public int MaxFlyAltitude { get => m_maxFlyAltitude; set => m_maxFlyAltitude = value; }
        private bool m_isLava;
        private int m_bonusXP;
        private int m_bonusRP;
        private int m_bonusBP;
        private int m_bonusCoin;
        private float m_tensionRate;
        private eRealm m_realm;

        public Zone(Region region, ushort id, string desc, int xoff, int yoff, int width, int height, ushort zoneskinID,
            bool isDivingEnabled, int waterlevel, bool islava, int xpBonus, int rpBonus, int bpBonus, int coinBonus,
            byte realm, bool allowMagicalItem, bool allowReputation, float tensionRate, bool isDungeon, int maxFlyAltitude = 10000)
        {
            m_Region = region;
            m_ID = id;
            m_Description = desc;
            Offset = Vector.Create(xoff, yoff);
            m_Width = width;
            m_Height = height;
            m_zoneSkinID = zoneskinID;
            m_waterlevel = waterlevel;
            m_isDivingEnabled = isDivingEnabled;
            m_maxFlyAltitude = maxFlyAltitude;
            m_isLava = islava;
            m_bonusXP = xpBonus;
            m_bonusRP = rpBonus;
            m_bonusBP = bpBonus;
            m_bonusCoin = coinBonus;
            AllowMagicalItem = allowMagicalItem;
            m_tensionRate = tensionRate;
            m_realm = (eRealm)realm;
            m_allowReputation = allowReputation;
            m_isDungeon = isDungeon;
        }

        public void Delete()
        {
            Array.Clear(_subZones, 0, _subZones.Length);
            m_Region = null;
            DOL.Events.GameEventMgr.RemoveAllHandlersForObject(this);
        }

        public virtual LanguageDataObject.eTranslationIdentifier TranslationIdentifier => LanguageDataObject.eTranslationIdentifier.eZone;
        public string TranslationId { get => ID.ToString(); set { } }
        public eRealm Realm => m_realm;
        public bool IsDungeon { get => m_isDungeon; set => m_isDungeon = value; }
        public Region ZoneRegion { get => m_Region; set => m_Region = value; }
        public ushort ID => m_ID;
        public ushort ZoneSkinID => m_zoneSkinID;
        public string Description { get => m_Description; set => m_Description = value; }
        public int Width => m_Width;
        public int Height => m_Height;
        public int Waterlevel { get => m_waterlevel; set => m_waterlevel = value; }
        public bool AllowMagicalItem { get; set; }
        public bool IsDivingEnabled { get => m_isDivingEnabled; set => m_isDivingEnabled = value; }
        public virtual bool IsLava { get => m_isLava; set => m_isLava = value; }
        public int TotalNumberOfObjects => _objectCount;
        public bool IsPathingEnabled { get; set; }

        #region Zone Bonuses
        /// <summary>
        /// Bonus XP Factor
        /// </summary>
        public int BonusExperience
        {
            get { return m_bonusXP; }
            set { m_bonusXP = value; }
        }
        /// <summary>
        /// Bonus RP Gained (%)
        /// </summary>
        public int BonusRealmpoints
        {
            get { return m_bonusRP; }
            set { m_bonusRP = value; }
        }
        /// <summary>
        /// Bonus BP Gained (%)
        /// </summary>
        public int BonusBountypoints
        {
            get { return m_bonusBP; }
            set { m_bonusBP = value; }
        }
        /// <summary>
        /// Bonus Money Gained (%)
        /// </summary>
        public int BonusCoin
        {
            get { return m_bonusCoin; }
            set { m_bonusCoin = value; }
        }

        public bool AllowReputation { get => m_allowReputation; set => m_allowReputation = value; }

        /// <summary>
        /// Rate to multiply player tension gains by
        /// </summary>
        public float TensionRate
        {
            get { return m_tensionRate; }
            set { m_tensionRate = value; }
        }
        #endregion

        public void OnObjectAddedToZone() { Interlocked.Increment(ref _objectCount); }
        public void OnObjectRemovedFromZone() { Interlocked.Decrement(ref _objectCount); }

        private static short GetSubZoneOffset(int lineSubZoneIndex, int columnSubZoneIndex)
        {
            return (short)(columnSubZoneIndex + (lineSubZoneIndex << SUBZONE_ARRAY_Y_SHIFT));
        }

        private short GetSubZoneIndex(Coordinate loc)
        {
            int xDiff = loc.X - Offset.X;
            int yDiff = loc.Y - Offset.Y;

            if (xDiff < 0 || xDiff > 65535 || yDiff < 0 || yDiff > 65535)
                return -1;

            return GetSubZoneOffset(yDiff >> SUBZONE_SHIFT, xDiff >> SUBZONE_SHIFT);
        }

        private SubZone GetOrCreateSubZone(int index)
        {
            SubZone subZone = _subZones[index];

            if (subZone != null)
                return subZone;

            SubZone created = new(this);
            return Interlocked.CompareExchange(ref _subZones[index], created, null) ?? created;
        }

        /// <summary>
        /// Called by GameObject.AddToWorld. Queues the object for insertion (processed by ZoneService).
        /// </summary>
        public void ObjectEnterZone(GameObject obj)
        {
            int subZoneIndex = GetSubZoneIndex(obj.Coordinate);

            if (subZoneIndex < 0 || subZoneIndex >= SUBZONE_NBR)
            {
                if (log.IsDebugEnabled)
                    log.Debug($"Object {obj.Name} ({obj.ObjectID}) is out of zone bounds on enter (zone {ID})");
                return;
            }

            obj.SubZoneObject.InitiateSubZoneTransition(this, GetOrCreateSubZone(subZoneIndex));
        }

        /// <summary>
        /// Periodic relocation sweep, driven by RelocationService via Region.Relocate().
        /// </summary>
        internal void Relocate(object state)
        {
            if (_objectCount <= 0)
                return;

            Region region = m_Region;

            if (region == null)
                return;

            for (int s = 0; s < SUBZONE_NBR; s++)
            {
                SubZone subZone = _subZones[s];

                if (subZone == null)
                    continue;

                for (int t = 0; t < 4; t++)
                {
                    WriteLockedLinkedList<GameObject> list = subZone[(eGameObjectType)t];

                    if (list.Count == 0)
                        continue;

                    lock (list.Lock)
                    {
                        for (LinkedListNode<GameObject> node = list.First; node != null; node = node.Next)
                        {
                            GameObject obj = node.Value;
                            SubZoneObject szo = obj.SubZoneObject;

                            if (szo.CurrentSubZone != subZone)
                                continue; 

                            if (obj.ObjectState != GameObject.eObjectState.Active || obj.CurrentRegion != region)
                            {
                                szo.InitiateSubZoneTransition(null, null);
                                continue;
                            }

                            int newIndex = GetSubZoneIndex(obj.Coordinate);

                            if (newIndex >= 0)
                            {
                                if (_subZones[newIndex] != subZone)
                                    szo.InitiateSubZoneTransition(this, GetOrCreateSubZone(newIndex));

                                continue;
                            }

                            Zone newZone = region.GetZone(obj.Coordinate);

                            if (newZone == null || newZone == this)
                            {
                                szo.InitiateSubZoneTransition(null, null);
                                continue;
                            }

                            int idxInNewZone = newZone.GetSubZoneIndex(obj.Coordinate);

                            if (idxInNewZone < 0)
                                szo.InitiateSubZoneTransition(null, null);
                            else
                                szo.InitiateSubZoneTransition(newZone, newZone.GetOrCreateSubZone(idxInNewZone));
                        }
                    }
                }
            }
        }

        internal ArrayList GetObjectsInRadius(eGameObjectType type, Coordinate coordinate, ushort radius, ArrayList partialList, bool ignoreZ)
        {
            uint sqRadius = (uint)radius * radius;
            int xInZone = coordinate.X - Offset.X;
            int yInZone = coordinate.Y - Offset.Y;
            int cellNbr = (radius >> SUBZONE_SHIFT) + 1;
            int xInCell = xInZone >> SUBZONE_SHIFT;
            int yInCell = yInZone >> SUBZONE_SHIFT;

            int minColumn = Math.Max(0, xInCell - cellNbr);
            int maxColumn = Math.Min(SUBZONE_NBR_ON_ZONE_SIDE - 1, xInCell + cellNbr);
            int minLine = Math.Max(0, yInCell - cellNbr);
            int maxLine = Math.Min(SUBZONE_NBR_ON_ZONE_SIDE - 1, yInCell + cellNbr);

            int referenceIndex = GetSubZoneIndex(coordinate);

            for (int line = minLine; line <= maxLine; line++)
            {
                for (int column = minColumn; column <= maxColumn; column++)
                {
                    int index = GetSubZoneOffset(line, column);
                    SubZone subZone = _subZones[index];

                    if (subZone == null)
                        continue;

                    WriteLockedLinkedList<GameObject> list = subZone[type];

                    if (list.Count == 0)
                        continue;

                    bool checkDistance = true;

                    if (index != referenceIndex)
                    {
                        int xLeft = column << SUBZONE_SHIFT;
                        int xRight = xLeft + SUBZONE_SIZE;
                        int yTop = line << SUBZONE_SHIFT;
                        int yBottom = yTop + SUBZONE_SIZE;

                        if (!CheckMinDistance(xInZone, yInZone, xLeft, xRight, yTop, yBottom, sqRadius))
                            continue;

                        if (CheckMaxDistance(xInZone, yInZone, xLeft, xRight, yTop, yBottom, sqRadius))
                            checkDistance = false;
                    }

                    lock (list.Lock)
                    {
                        for (LinkedListNode<GameObject> node = list.First; node != null; node = node.Next)
                        {
                            GameObject obj = node.Value;

                            if (obj == null || obj.ObjectState != GameObject.eObjectState.Active)
                                continue;

                            if (!checkDistance || CheckSquareDistance(coordinate, obj.Coordinate, sqRadius, ignoreZ))
                                partialList.Add(obj);
                        }
                    }
                }
            }

            return partialList;
        }

        public static bool CheckSquareDistance(Coordinate locA, Coordinate locB, uint squaredDistance, bool ignoreZ)
        {
            int xDiff = locA.X - locB.X;
            long dist = (long)xDiff * xDiff;
            if (dist > squaredDistance) return false;
            int yDiff = locA.Y - locB.Y;
            dist += (long)yDiff * yDiff;
            if (dist > squaredDistance) return false;
            if (!ignoreZ)
            {
                int zDiff = locA.Z - locB.Z;
                dist += (long)zDiff * zDiff;
            }
            return dist <= squaredDistance;
        }

        private static bool CheckMinDistance(int x, int y, int xLeft, int xRight, int yTop, int yBottom, uint squareRadius)
        {
            long distance;
            if (y >= yTop && y <= yBottom)
            {
                int xdiff = Math.Min(Math.Abs(x - xLeft), Math.Abs(x - xRight));
                distance = (long)xdiff * xdiff;
            }
            else if (x >= xLeft && x <= xRight)
            {
                int ydiff = Math.Min(Math.Abs(y - yTop), Math.Abs(y - yBottom));
                distance = (long)ydiff * ydiff;
            }
            else
            {
                int xdiff = Math.Min(Math.Abs(x - xLeft), Math.Abs(x - xRight));
                int ydiff = Math.Min(Math.Abs(y - yTop), Math.Abs(y - yBottom));
                distance = (long)xdiff * xdiff + (long)ydiff * ydiff;
            }
            return distance <= squareRadius;
        }

        private static bool CheckMaxDistance(int x, int y, int xLeft, int xRight, int yTop, int yBottom, uint squareRadius)
        {
            int xdiff = Math.Max(Math.Abs(x - xLeft), Math.Abs(x - xRight));
            int ydiff = Math.Max(Math.Abs(y - yTop), Math.Abs(y - yBottom));
            return (long)xdiff * xdiff + (long)ydiff * ydiff <= squareRadius;
        }

        public IList<IArea> GetAreas() { return m_Region.GetAreasOfZone(this); }
        public IList<IArea> GetAreasOfSpot(Coordinate spot) => m_Region.GetAreasOfZone(this, spot, true);
        public IList<IArea> GetAreas(Predicate<IArea> predicate) { return m_Region.GetAreasOfZone(this, predicate); }

        #region Get random NPC

        /// <summary>
        /// Get's a random NPC based on a con level
        /// </summary>
        /// <param name="realm"></param>
        /// <param name="compareLevel"></param>
        /// <param name="conLevel">-3 grey, -2 green, -1 blue, 0 yellow, 1 - orange, 2 red, 3 purple</param>
        /// <returns></returns>
        public GameNPC GetRandomNPCByCon(eRealm realm, int compareLevel, int conLevel)
        {
            List<GameNPC> npcs = GetNPCsOfZone(new eRealm[] { realm }, 0, 0, compareLevel, conLevel, true);
            GameNPC randomNPC = (npcs.Count == 0 ? null : npcs[0]);
            return randomNPC;
        }

        /// <summary>
        /// Get a random NPC belonging to a realm
        /// </summary>
        /// <param name="realm">The realm the NPC belong to</param>
        /// <returns>a npc</returns>
        public GameNPC GetRandomNPC(eRealm realm)
        {
            return GetRandomNPC(new eRealm[] { realm }, -1, -1);
        }

        /// <summary>
        /// Get a random NPC belonging to a realm between levels minlevel and maxlevel
        /// </summary>
        /// <param name="realm">The realm the NPC belong to</param>
        /// <param name="minLevel">The minimal level of the NPC</param>
        /// <param name="maxLevel">The maximal level NPC</param>
        /// <returns>A npc</returns>
        public GameNPC GetRandomNPC(eRealm realm, int minLevel, int maxLevel)
        {
            return GetRandomNPC(new eRealm[] { realm }, minLevel, maxLevel);
        }

        /// <summary>
        /// Get a random npc from zone with given realms
        /// </summary>
        /// <param name="realms">The realms to get the NPC from</param>
        /// <returns>The NPC</returns>
        public GameNPC GetRandomNPC(eRealm[] realms)
        {
            return GetRandomNPC(realms, -1, -1);
        }

        /// <summary>
        /// Get a random npc from zone with given realms
        /// </summary>
        /// <param name="realms">The realms to get the NPC from</param>
        /// <param name="minLevel">The minimal level of the NPC</param>
        /// <param name="maxLevel">The maximum level of the NPC</param>
        /// <returns>The NPC</returns>
        public GameNPC GetRandomNPC(eRealm[] realms, int minLevel, int maxLevel)
        {
            List<GameNPC> npcs = GetNPCsOfZone(realms, minLevel, maxLevel, 0, 0, true);
            GameNPC randomNPC = (npcs.Count == 0 ? null : npcs[0]);
            return randomNPC;
        }

        /// <summary>
        /// Gets all NPC's in zone
        /// </summary>
        /// <param name="realm"></param>
        /// <returns></returns>
        public List<GameNPC> GetNPCsOfZone(eRealm realm)
        {
            return GetNPCsOfZone(new eRealm[] { realm }, 0, 0, 0, 0, false);
        }

        /// <summary>
        /// Get NPCs of a zone given various parameters
        /// </summary>
        /// <param name="realms"></param>
        /// <param name="minLevel"></param>
        /// <param name="maxLevel"></param>
        /// <param name="compareLevel"></param>
        /// <param name="conLevel"></param>
        /// <param name="firstOnly"></param>
        /// <returns></returns>
        public List<GameNPC> GetNPCsOfZone(eRealm[] realms, int minLevel, int maxLevel, int compareLevel, int conLevel, bool firstOnly)
        {
            List<GameNPC> list = new();

            try
            {
                foreach (SubZone subZone in _subZones)
                {
                    if (subZone == null)
                        continue;

                    WriteLockedLinkedList<GameObject> npcs = subZone[eGameObjectType.NPC];

                    if (npcs.Count == 0)
                        continue;

                    lock (npcs.Lock)
                    {
                        for (LinkedListNode<GameObject> node = npcs.First; node != null; node = node.Next)
                        {
                            if (node.Value is not GameNPC currentNPC || currentNPC.ObjectState != GameObject.eObjectState.Active)
                                continue;

                            for (int i = 0; i < realms.Length; ++i)
                            {
                                if (currentNPC.Realm != realms[i])
                                    continue;

                                bool addToList = true;

                                if (compareLevel > 0 && conLevel > 0)
                                    addToList = (int)GameObject.GetConLevel(compareLevel, currentNPC.Level) == conLevel;
                                else
                                {
                                    if (minLevel > 0 && currentNPC.Level < minLevel) addToList = false;
                                    if (maxLevel > 0 && currentNPC.Level > maxLevel) addToList = false;
                                }

                                if (addToList)
                                {
                                    list.Add(currentNPC);

                                    if (firstOnly)
                                        return list;
                                }

                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("GetNPCsOfZone: Caught Exception for zone " + Description + ".", ex);
            }

            return list;
        }

        #endregion
    }
}