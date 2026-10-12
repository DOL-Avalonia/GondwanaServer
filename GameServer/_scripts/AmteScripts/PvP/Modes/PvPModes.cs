using AmteScripts.Managers;
using DOL.Database;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;

namespace AmteScripts.PvP.Modes
{
    /// Immutable per-session snapshot. Swapped atomically by PvpService → lock-free readers.
    public sealed class PvPSessionContext
    {
        public PvpSession Session { get; }
        public PvpManager.eSessionTypes Type { get; }
        public ushort RegionID { get; }
        public IReadOnlyList<Zone> Zones { get; }
        public IReadOnlyList<GameNPC> SpawnNpcs { get; }
        public PvPParticipants Participants { get; }
        public PvPScoreboard Scoreboard { get; }
        public int TempAreaRadius => Session.TempAreaRadius;
        public Region Region => WorldMgr.GetRegion(RegionID);

        /// <summary>
        /// Map pins of the session. Modes only set pin states, PvpService refreshes/sends them.
        /// </summary>
        public PvPMapPins MapPins { get; }

        public PvPSessionContext(PvpSession session, IReadOnlyList<Zone> zones, IReadOnlyList<GameNPC> spawnNpcs, PvPParticipants participants, PvPScoreboard scoreboard)
        {
            Session = session;
            Type = (PvpManager.eSessionTypes)session.SessionType;
            Zones = zones;
            SpawnNpcs = spawnNpcs;
            Participants = participants;
            Scoreboard = scoreboard;
            RegionID = zones.Count > 0 ? zones[0].ZoneRegion.ID : (ushort)0;
            MapPins = new PvPMapPins(participants);
        }

        public bool IsSessionZone(Zone zone)
        {
            for (int i = 0; i < Zones.Count; i++) if (Zones[i] == zone) return true;
            return false;
        }

        /// Region.Objects based: valid even before the game loop has built the SubZone index.
        public GameNPC FindNpc(Func<GameNPC, bool> predicate)
        {
            foreach (Zone zone in Zones)
                foreach (GameObject obj in zone.ZoneRegion.Objects)
                    if (obj is GameNPC npc && npc.ObjectState == GameObject.eObjectState.Active && npc.CurrentZone == zone && predicate(npc))
                        return npc;
            return null;
        }

        public List<GameNPC> FindNpcs(Func<GameNPC, bool> predicate)
        {
            List<GameNPC> result = new();
            foreach (Zone zone in Zones)
                foreach (GameObject obj in zone.ZoneRegion.Objects)
                    if (obj is GameNPC npc && npc.ObjectState == GameObject.eObjectState.Active && npc.CurrentZone == zone && predicate(npc))
                        result.Add(npc);
            return result;
        }

        /// <summary>
        /// All active objects of type T in the session region (region-wide, dropped flags can be anywhere).
        /// </summary>
        public List<T> FindObjects<T>(Func<T, bool> predicate = null) where T : GameObject
        {
            List<T> result = new();
            Region region = Region;
            if (region == null) return result;
            GameObject[] objects = region.Objects;
            for (int i = 0; i < objects.Length; i++)
                if (objects[i] is T obj && obj.ObjectState == GameObject.eObjectState.Active && (predicate == null || predicate(obj)))
                    result.Add(obj);
            return result;
        }

        public void Broadcast(string languageKey, eChatType type, eChatLoc loc = eChatLoc.CL_SystemWindow)
        {
            foreach (GamePlayer p in Participants.Players)
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Language, languageKey), type, loc);
        }
    }

    /// Module-thread only. Replaces WorldMgr.GetAllPlayersInPvP() scans and the drifting _playersInPvP counter.
    public sealed class PvPParticipants
    {
        private readonly List<GamePlayer> _players = new(64);
        private readonly HashSet<GamePlayer> _set = new();

        /// Live list: call Snapshot() before running code that may add/remove participants.
        public IReadOnlyList<GamePlayer> Players => _players;
        public int Count => _players.Count;
        public bool Contains(GamePlayer p) => _set.Contains(p);
        public bool Add(GamePlayer p) { if (!_set.Add(p)) return false; _players.Add(p); p.IsInPvP = true; return true; }
        public bool Remove(GamePlayer p) { if (!_set.Remove(p)) return false; _players.Remove(p); p.IsInPvP = false; return true; }
        public void Snapshot(List<GamePlayer> buffer) { buffer.Clear(); buffer.AddRange(_players); }
    }

    public abstract class PvPGameModeBase
    {
        protected readonly ILog log;
        protected PvPGameModeBase() { log = LogManager.GetLogger(GetType()); }

        public abstract PvpManager.eSessionTypes Type { get; }
        public bool IsActive { get; private set; }
        protected PvPSessionContext Ctx { get; private set; }
        public virtual int KillBasePoints => 2;
        public virtual bool KillHasRr5Bonus => false;
        public virtual bool OutpostIsSafeArea => false;
        public virtual List<GameStaticItem> CreateOutpostItems(Position center, GamePlayer owner) => null;

        public void Open(PvPSessionContext ctx)
        {
            Ctx = ctx; IsActive = true;
            try { OnOpen(); } catch (Exception e) { log.Error($"[{Type}] OnOpen failed", e); }
        }

        public void Close()
        {
            if (!IsActive) return;
            try { OnClose(); }
            catch (Exception e) { log.Error($"[{Type}] OnClose failed", e); }
            finally { IsActive = false; Ctx = null; }
        }

        protected abstract void OnOpen();
        protected abstract void OnClose();
        public virtual int Tick(long now) => 30_000;
        public virtual void OnPlayerJoined(GamePlayer player) { }
        public virtual void OnPlayerLeft(GamePlayer player) { }
        public virtual void OnPlayerLinkdead(GamePlayer player) { }
        public virtual void OnPlayerRegionEntered(GamePlayer player) { }
        protected static int Until(long deadline, long now) => (int)Math.Clamp(deadline - now, 0, int.MaxValue);
    }
}