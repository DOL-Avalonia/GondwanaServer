using AmteScripts.Managers;
using DOL.Database;
using DOL.GS;
using DOL.GS.Geometry;
using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace AmteScripts.PvP.Modes
{
    /// <summary>
    /// Owns the map markers of the running PvP session.
    /// Two backends:
    ///  - Relic:      MinotaurRelicMapUpdate/Remove (ids 1..39). Rendered by the client only for relic ids it knows,
    ///                so use it for "landmark" pins (hill, bosses, territories) with ids known to work.
    ///  - Objective:  QuestEntry map objective (ids 40..49). Renders any id, custom name/description,
    ///                zone-aware coordinates. Use it for moving pins (flag carriers).
    /// </summary>
    public sealed class PvPMapPins
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public enum PinKind : byte { Relic, Objective }

        public const int REFRESH_MS = 2000;
        private const byte RELIC_FIRST_ID = 1, RELIC_LAST_ID = 39;
        private const byte OBJ_FIRST_ID = 40, OBJ_LAST_ID = 49;

        private sealed class Pin
        {
            public byte Id;
            public PinKind Kind;
            public Position Position = Position.Nowhere;
            public Position LastSent = Position.Nowhere;
            public bool Visible;
            public Func<GamePlayer, bool> Filter;
            public readonly HashSet<string> ShownTo = new();
            public string NameKey;
            public string DescKey;
            public string LastSentNameKey;
        }

        private readonly PvPParticipants _participants;
        private readonly Dictionary<byte, Pin> _pins = new();
        private readonly SortedSet<byte> _freeRelic = new();
        private readonly SortedSet<byte> _freeObjective = new();
        private long _nextRefreshAt;

        public int FreeCount => _freeRelic.Count + _freeObjective.Count;

        public PvPMapPins(PvPParticipants participants)
        {
            _participants = participants;
            HashSet<byte> reserved = ReservedRelicIds();
            for (int id = RELIC_FIRST_ID; id <= RELIC_LAST_ID; id++)
                if (!reserved.Contains((byte)id)) _freeRelic.Add((byte)id);
            for (int id = OBJ_FIRST_ID; id <= OBJ_LAST_ID; id++)
                _freeObjective.Add((byte)id);
            if (log.IsInfoEnabled)
                log.Info($"[PvP] Map pins: {_freeRelic.Count} relic ids, {_freeObjective.Count} objective ids (relic ids reserved: {string.Join(",", reserved)})");
        }

        private static HashSet<byte> ReservedRelicIds()
        {
            HashSet<byte> set = new();
            try
            {
                foreach (DBMinotaurRelic relic in GameServer.Database.SelectAllObjects<DBMinotaurRelic>())
                    if (relic.RelicID > 0 && relic.RelicID <= byte.MaxValue) set.Add((byte)relic.RelicID);
            }
            catch (Exception e) { log.Warn("[PvP] Could not read DBMinotaurRelic ids", e); }
            foreach (int id in TerritoryRelicManager.ActiveRelics.Keys)
                if (id > 0 && id <= byte.MaxValue) set.Add((byte)id);
            return set;
        }

        #region allocation

        public bool TryAllocate(out byte id, PinKind kind = PinKind.Relic)
        {
            SortedSet<byte> pool = kind == PinKind.Relic ? _freeRelic : _freeObjective;
            if (pool.Count == 0)
            {
                id = 0;
                log.Warn($"[PvP] No free {kind} map pin id left");
                return false;
            }
            id = pool.Min;
            pool.Remove(id);
            _pins[id] = new Pin { Id = id, Kind = kind };
            return true;
        }

        public void Release(byte id)
        {
            if (!_pins.Remove(id, out Pin pin)) return;
            RemoveFromAll(pin);
            (pin.Kind == PinKind.Relic ? _freeRelic : _freeObjective).Add(id);
        }

        public bool TryGetObjectiveKeys(byte id, out string nameKey, out string descKey)
        {
            if (_pins.TryGetValue(id, out Pin pin) && pin.Kind == PinKind.Objective)
            {
                nameKey = pin.NameKey;
                descKey = pin.DescKey;
                return true;
            }
            nameKey = null; descKey = null;
            return false;
        }

        public void Set(byte id, Position position, bool visible, Func<GamePlayer, bool> filter = null, string nameKey = null, string descKey = null)
        {
            if (!_pins.TryGetValue(id, out Pin pin)) return;
            pin.Position = position;
            pin.Visible = visible && position != Position.Nowhere && position.RegionID != 0;
            pin.Filter = filter;
            if (nameKey != null) pin.NameKey = nameKey;
            if (descKey != null) pin.DescKey = descKey;
        }

        public void Hide(byte id)
        {
            if (_pins.TryGetValue(id, out Pin pin)) pin.Visible = false;
        }

        #endregion

        #region refresh

        public int Tick(long now)
        {
            if (_pins.Count == 0) return int.MaxValue;
            if (now < _nextRefreshAt) return (int)(_nextRefreshAt - now);
            _nextRefreshAt = now + REFRESH_MS;
            Refresh();
            return REFRESH_MS;
        }

        public void Refresh()
        {
            IReadOnlyList<GamePlayer> players = _participants.Players;
            foreach (Pin pin in _pins.Values)
            {
                for (int i = 0; i < players.Count; i++) Send(pin, players[i], false);
                pin.LastSent = pin.Position;
                pin.LastSentNameKey = pin.NameKey;
            }
        }

        public void SendAll(GamePlayer player)
        {
            foreach (Pin pin in _pins.Values) Send(pin, player, true);
        }

        public void RemoveAll(GamePlayer player)
        {
            foreach (Pin pin in _pins.Values)
                if (pin.ShownTo.Remove(player.InternalID) && player.ObjectState == GameObject.eObjectState.Active)
                    SendRemove(pin, player);
        }

        public void Clear()
        {
            foreach (Pin pin in _pins.Values)
            {
                RemoveFromAll(pin);
                (pin.Kind == PinKind.Relic ? _freeRelic : _freeObjective).Add(pin.Id);
            }
            _pins.Clear();
        }

        private void RemoveFromAll(Pin pin)
        {
            IReadOnlyList<GamePlayer> players = _participants.Players;
            for (int i = 0; i < players.Count; i++)
            {
                GamePlayer p = players[i];
                if (pin.ShownTo.Remove(p.InternalID) && p.ObjectState == GameObject.eObjectState.Active)
                    SendRemove(pin, p);
            }
            pin.ShownTo.Clear();
        }

        private static void Send(Pin pin, GamePlayer p, bool force)
        {
            if (p.ObjectState != GameObject.eObjectState.Active) return;

            bool show = pin.Visible
                     && p.CurrentRegionID == pin.Position.RegionID
                     && (pin.Filter == null || pin.Filter(p));

            if (show)
            {
                bool alreadyShown = pin.ShownTo.Contains(p.InternalID);
                bool textChanged = pin.Kind == PinKind.Objective && pin.LastSentNameKey != pin.NameKey;

                // objective packets carry strings: only resend when the position actually moved
                if (pin.Kind == PinKind.Objective && alreadyShown && !force
                    && pin.LastSent.RegionID == pin.Position.RegionID
                    && pin.LastSent.Coordinate == pin.Position.Coordinate
                    && !textChanged)
                    return;

                if (pin.Kind == PinKind.Relic)
                {
                    p.Out.SendMinotaurRelicMapUpdate(pin.Id, pin.Position);
                }
                else
                {
                    if (alreadyShown && textChanged)
                        p.Out.ClearMapObjective(pin.Id);

                    p.Out.SendMapObjective(pin.Id, pin.Position);
                }

                pin.ShownTo.Add(p.InternalID);
            }
            else if (pin.ShownTo.Remove(p.InternalID))
            {
                SendRemove(pin, p);
            }
        }

        private static void SendRemove(Pin pin, GamePlayer p)
        {
            if (pin.Kind == PinKind.Relic) p.Out.SendMinotaurRelicMapRemove(pin.Id);
            else p.Out.ClearMapObjective(pin.Id);
        }

        #endregion
    }
}