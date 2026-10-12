using AmteScripts.Areas;
using AmteScripts.PvP;
using AmteScripts.PvP.Modes;
using DOL.Database;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.Territories;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace AmteScripts.Managers
{
    public class PvpManager
    {
        public enum eSessionTypes
        {
            None = 0,
            Deathmatch = 1,
            CaptureTheFlag = 2,
            TreasureHunt = 3,
            BringAFriend = 4,
            TerritoryCapture = 5,
            BossHunt = 6,
            KingOfTheHill = 7,
            CoreRun = 8,
            Biohazard = 9,
        }

        public record Spawn(GameNPC NPC, Position Position)
        {
            public Spawn() : this(null, Position.Nowhere) { }
            public Spawn(GameNPC npc) : this(npc, Position.Nowhere) { }
            public Spawn(Position pos) : this(null, pos) { }
        }

        public static PvpManager Instance { get; } = new();
        internal static readonly string ScoreDirectory = "temp";
        internal static readonly string ScoreFilePath = Path.Combine(ScoreDirectory, "PvPScore.dat");
        internal static readonly object ScoreFileLock = new();
        private static PvpService Svc => PvpService.Instance;

        private static void Sync(Action<PvpService> action) => ModuleService.Sync(Svc.Gate, () => action(Svc));
        private static T Query<T>(Func<PvpService, T> func) => ModuleService.Query(Svc.Gate, () => func(Svc));

        // ---- lock-free reads (immutable session context)
        public bool IsOpen => Svc.IsOpen;
        public PvpSession CurrentSession => Svc.CurrentSession;
        public eSessionTypes CurrentSessionType => Svc.CurrentSessionType;
        public string CurrentSessionId => string.IsNullOrEmpty(CurrentSession?.SessionID) ? "(none)" : CurrentSession.SessionID;
        public IReadOnlyList<Zone> CurrentZones => Svc.CurrentZones;
        public Region CurrentRegion => Svc.Context?.Region;
        public bool AllowsGroups => Svc.AllowsGroups;
        public bool AllowsSolo => Svc.AllowsSolo;
        public int MaxGroupSize => Svc.MaxGroupSize;
        public bool IsPredatorEnabled => Svc.IsPredatorEnabled;
        public int PeakPlayerCount => Svc.PeakPlayerCount;
        public ImmutableArray<int> DefaultEmblems => ImmutableArray.Create(5061, 6645, 84471, 6272, 55302, 64792, 111402, 39859, 21509, 123019);
        public int GetEmblemForPlayer(GamePlayer p) => Svc.GetEmblemForPlayer(p);
        public int GetPlayerSoloEmblem(GamePlayer p) => Svc.GetPlayerSoloEmblem(p);
        public bool IsIn(GameLiving living) => living is GamePlayer p && p.IsInPvP;
        public bool IsPlayerInQueue(GamePlayer p) => p.TempProperties.getProperty<bool>("PvPQueued", false);
        public bool IsPvPRegion(ushort regionID) => Svc.Context?.RegionID == regionID;
        public bool IsInActivePvpZone(GamePlayer p) => p?.CurrentZone != null && (Svc.Context?.IsSessionZone(p.CurrentZone) ?? false);
        public void SendPvPRules(GamePlayer p) => Svc.SendPvPRules(p);
        public static bool CanGroup(GamePlayer source, GamePlayer target, bool quiet = false) => Svc.CanGroup(source, target, quiet);

        // ---- writes: posted to the module context
        public bool Open(string sessionID, bool force) => Query(s => s.OpenInternal(sessionID, force));
        public bool Close() => Query(s => s.CloseInternal());
        public bool AddPlayer(GamePlayer p) { ModuleService.Run(static s => s.svc.AddPlayerInternal(s.p), (svc: Svc, p)); return true; }
        public bool AddGroup(GamePlayer leader) { ModuleService.Run(static s => s.svc.AddGroupInternal(s.leader), (svc: Svc, leader)); return true; }
        public void KickPlayer(GamePlayer p, bool disband = true) => ModuleService.Run(static s => s.svc.KickPlayerInternal(s.p, s.disband), (svc: Svc, p, disband));
        public void CleanupPlayer(GamePlayer p, bool disband = true) => ModuleService.Run(static s => { if (s.p.IsInPvP) s.svc.CleanupPlayerInternal(s.p, s.disband); }, (svc: Svc, p, disband));
        public void PlayerLinkDeath(GamePlayer p) => ModuleService.Run(static s => s.svc.PlayerLinkDeathInternal(s.p), (svc: Svc, p));
        public void OnPlayerQuit(GamePlayer p) => Sync(svc => svc.OnPlayerQuitInternal(p));
        public void EnqueueSolo(GamePlayer p) => ModuleService.Run(static s => s.svc.EnqueueSoloInternal(s.p), (svc: Svc, p));
        public void DequeueSolo(GamePlayer p) => ModuleService.Run(static s => s.svc.DequeueSoloInternal(s.p), (svc: Svc, p));
        public void OnMemberJoinGuild(Guild g, GamePlayer p) => ModuleService.Run(static s => s.svc.OnMemberJoinGuildInternal(s.g, s.p), (svc: Svc, g, p));
        public void OnMemberLeaveGuild(Guild g, GamePlayer p) => ModuleService.Run(static s => s.svc.OnMemberLeaveGuildInternal(s.g, s.p), (svc: Svc, g, p));
        public void OnMemberJoinGroup(Group g, GamePlayer p) => ModuleService.Run(static s => s.svc.OnMemberJoinGroupInternal(s.g, s.p), (svc: Svc, g, p));
        public void OnMemberLeaveGroup(Group g, GamePlayer p) => ModuleService.Run(static s => s.svc.OnMemberLeaveGroupInternal(s.g, s.p), (svc: Svc, g, p));
        public void OnGuildRename(Guild g) => ModuleService.Run(static s => s.svc.OnGuildRenameInternal(s.g), (svc: Svc, g));
        public void HandlePlayerKill(GamePlayer k, GamePlayer v)
        {
            if (!Svc.IsOpen || !k.IsInPvP || !v.IsInPvP) return;
            ModuleService.Run(static s => s.svc.HandlePlayerKillInternal(s.k, s.v), (svc: Svc, k, v));
        }
        public void HandleBossHit(GamePlayer p, GameNPC boss, int dmg) => ModuleService.Run(static s => (s.svc.Mode as BossHuntMode)?.OnBossHit(s.p, s.boss, s.dmg), (svc: Svc, p, boss, dmg));
        public void HandleBossKill(GamePlayer k, GameNPC boss) => ModuleService.Run(static s => s.svc.HandleBossKillInternal(s.k, s.boss), (svc: Svc, k, boss));
        public void HandleCoreDelivery(GamePlayer p, int points) => ModuleService.Run(static s => s.svc.HandleCoreDeliveryInternal(s.p, s.points), (svc: Svc, p, points));
        public void HandleBiohazardDelivery(GamePlayer p, int points) => ModuleService.Run(static s => s.svc.Scoreboard.Award(s.p, x => x.Biohazard_SamplesReturned += s.points), (svc: Svc, p, points));
        public void RespawnSingleBiohazardChest(DOL.GS.Scripts.BiohazardChest old) => ModuleService.Run(static s => (s.svc.Mode as BiohazardMode)?.RespawnSingleChest(s.old), (svc: Svc, old));
        public void AwardCTFCarrierKill(GamePlayer killer, GamePlayer carrier) => ModuleService.Run(static s => s.svc.Scoreboard.Award(s.killer, x => { x.Flag_KillFlagCarrierCount++; x.Flag_KillFlagCarrierPoints += 6; }), (svc: Svc, killer));
        public void AwardCTFOwnershipPoints(GamePlayer p, int points) => ModuleService.Run(static s => s.svc.Scoreboard.Award(s.p, x => x.Flag_OwnershipPoints += s.points), (svc: Svc, p, points));
        public void AwardCTFOwnershipPoints(Guild g, int points) => ModuleService.Run(static s => s.svc.Scoreboard.Award(s.g, x => x.Flag_OwnershipPoints += s.points), (svc: Svc, g, points));
        public void AwardCTFCapturePoints(GamePlayer p) => ModuleService.Run(static s => s.svc.Scoreboard.Award(s.p, x => { x.Flag_FlagReturnsCount++; x.Flag_FlagReturnsPoints += 20; }), (svc: Svc, p));
        public void NotifyFlagCarrier(GamePlayer p, int flagId, bool carrying)
        {
            if (p == null || Svc.CurrentSessionType != eSessionTypes.CaptureTheFlag) return;
            ModuleService.Run(static s =>
            {
                if (s.svc.Mode is not CaptureTheFlagMode m) return;
                if (s.carrying) m.OnFlagPickedUp(s.p, s.flagId); else m.OnFlagDropped(s.p, s.flagId);
            }, (svc: Svc, p, flagId, carrying));
        }
        public void NotifyFlagObject(PvP.CTF.GameFlagStatic flag, bool inWorld)
        {
            if (flag == null || Svc.CurrentSessionType != eSessionTypes.CaptureTheFlag) return;
            ModuleService.Run(static s =>
            {
                if (s.svc.Mode is not CaptureTheFlagMode m) return;
                if (s.inWorld) m.OnFlagSpawned(s.flag); else m.OnFlagRemoved(s.flag);
            }, (svc: Svc, flag, inWorld));
        }
        public (string NameKey, string DescKey) GetMapObjectiveKeys(int pinId)
        {
            return Query(s =>
            {
                if (s.Context?.MapPins != null && s.Context.MapPins.TryGetObjectiveKeys((byte)pinId, out string n, out string d))
                    return (n, d);

                return (null, null);
            });
        }
        public void AwardTerritoryCapturePoints(Territory t, GamePlayer p) => ModuleService.Run(static s => (s.svc.Mode as TerritoryCaptureMode)?.AwardCapturePoints(s.t, s.p), (svc: Svc, t, p));
        public void UpdateAllTerritoryMarkers(GamePlayer p = null) => ModuleService.Run(static s => (s.svc.Mode as TerritoryCaptureMode)?.UpdateAllTerritoryMarkers(s.p), (svc: Svc, p));
        public void SaveScores() => Svc.Scoreboard.Dirty = true;

        // ---- reads that touch module state: blocking hop when off-loop
        public PvPScore EnsureTotalScore(GamePlayer p) => Query(s => s.Scoreboard.EnsureTotal(p));
        public PvPScore EnsureSoloScore(GamePlayer p) => Query(s => s.Scoreboard.EnsureSolo(p));
        public PvPGroupScore EnsureGroupScore(Guild g) => Query(s => s.Scoreboard.EnsureGroup(g));
        public bool CanScore(GamePlayer p, bool quiet = false) => Query(s => s.Scoreboard.CanScore(p, quiet));
        public PvpTempArea FindSafeAreaForTarget(GamePlayer p) => Query(s => s.FindSafeAreaForTarget(p));
        public IList<string> GetStatistics(GamePlayer viewer, bool all = false) => Query(s => s.Scoreboard.GetStatistics(viewer, all));
    }
}