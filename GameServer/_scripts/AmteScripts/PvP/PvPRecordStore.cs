using DOL.Database;
using DOL.GS;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public static class PvPRecordStore
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
    private static readonly ConcurrentDictionary<string, RvrPlayer> _cache = new();
    private static readonly Dictionary<string, Task> _chains = new();
    private static readonly Lock _lock = new();

    public static RvrPlayer Get(string playerId) => _cache.TryGetValue(playerId, out RvrPlayer r) ? r : null;

    /// Login: read off-thread, continuation on the module thread.
    public static void Load(GamePlayer player, Action<GamePlayer, RvrPlayer> onLoaded)
    {
        string id = player.InternalID;
        Enqueue(id, () =>
        {
            RvrPlayer rec = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(id));
            if (rec != null) _cache[id] = rec; else _cache.TryRemove(id, out _);
            ModuleService.Instance.Post(static s => s.cb(s.player, s.rec), (cb: onLoaded, player, rec));
        });
    }

    /// Upsert. `record` stays the in-memory truth (SetPvPState/UpdatePvPState/RestorePlayerData read it, no SELECT).
    public static void Save(RvrPlayer record)
    {
        _cache[record.PlayerID] = record;
        RvrPlayer copy = record.Clone();
        Enqueue(record.PlayerID, () =>
        {
            RvrPlayer db = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(copy.PlayerID));
            if (db == null) GameServer.Database.AddObject(copy);
            else { copy.CopyTo(db); db.Dirty = true; GameServer.Database.SaveObject(db); }
        });
    }

    public static void Delete(string playerId)
    {
        _cache.TryRemove(playerId, out _);
        Enqueue(playerId, () =>
        {
            RvrPlayer db = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(playerId));
            if (db != null) GameServer.Database.DeleteObject(db);
        });
    }

    /// Fire-and-forget save of any DataObject (task stats etc.).
    public static void Persist(DataObject obj) => Task.Run(() => { try { obj.Dirty = true; GameServer.Database.SaveObject(obj); } catch (Exception e) { log.Error("Persist", e); } });

    public static void Evict(string playerId) => _cache.TryRemove(playerId, out _);

    private static void Enqueue(string id, Action work)
    {
        lock (_lock)
        {
            _chains.TryGetValue(id, out Task prev);
            Task next = prev == null ? Task.Run(() => Guarded(work)) : prev.ContinueWith(_ => Guarded(work), TaskScheduler.Default);
            _chains[id] = next;
            next.ContinueWith(t => { lock (_lock) { if (_chains.TryGetValue(id, out Task cur) && cur == t) _chains.Remove(id); } }, TaskScheduler.Default);
        }
    }

    private static void Guarded(Action work) { try { work(); } catch (Exception e) { log.Error("[PvPRecordStore] DB operation failed", e); } }
}