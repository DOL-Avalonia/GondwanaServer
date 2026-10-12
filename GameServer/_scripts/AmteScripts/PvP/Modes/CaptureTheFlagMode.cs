using AmteScripts.Managers;
using AmteScripts.PvP.CTF;
using DOL.GS;
using DOL.GS.Geometry;
using System;
using System.Collections.Generic;

namespace AmteScripts.PvP.Modes
{
    public sealed class CaptureTheFlagMode : PvPGameModeBase
    {
        public override PvpManager.eSessionTypes Type => PvpManager.eSessionTypes.CaptureTheFlag;

        public override List<GameStaticItem> CreateOutpostItems(Position center, GamePlayer owner)
            => PvPAreaOutposts.CreateCaptureFlagOutpostPad(center, owner);

        private const int TICK_INTERVAL = 1000;

        /// <summary>
        /// Relic outposts use 3000; set to WorldMgr.VISIBILITY_DISTANCE to mimic BossHunt exactly.
        /// </summary>
        private const int BASE_PAD_PROXIMITY = 1200;

        /// <summary>
        /// A flag that is neither carried nor in the world for this long is respawned on its pad.
        /// </summary>
        private const int MISSING_GRACE_MS = 5000;

        private enum FlagState : byte { Missing, AtBase, Carried, OnGround, OnTempPad }

        private sealed class FlagTracker
        {
            public int FlagID;
            public GameFlagBasePad Pad;
            public byte Pin;
            public bool HasPin;
            public GameFlagStatic WorldObject;
            public GamePlayer Carrier;
            public FlagState State;
            public long MissingSince;
            public Func<GamePlayer, bool> CarrierFilter;
            public GamePlayer CarrierFilterOwner;
        }

        private readonly List<FlagTracker> _flags = new();
        private readonly Dictionary<int, FlagTracker> _byId = new();
        private long _nextTickAt;

        #region lifecycle

        protected override void OnOpen()
        {
            int flagId = 0;

            foreach (GameNPC padSpawn in Ctx.FindNpcs(static n => n.Realm == eRealm.None && n.GuildName == "PVP"
                          && n.Name.StartsWith("PADSPAWN", StringComparison.OrdinalIgnoreCase)))
            {
                GameFlagBasePad pad = new()
                {
                    Position = padSpawn.Position,
                    Model = 2655,
                    Name = "BaseFlagPad",
                    FlagID = ++flagId
                };

                FlagTracker t = new() { FlagID = pad.FlagID, Pad = pad };
                t.HasPin = Ctx.MapPins.TryAllocate(out t.Pin, PvPMapPins.PinKind.Objective);
                if (!t.HasPin)
                    log.Warn($"[CTF] No free map pin for flag #{pad.FlagID}, it will not be shown on the map.");

                _flags.Add(t);
                _byId[pad.FlagID] = t;

                pad.AddToWorld();
            }

            if (_flags.Count == 0)
                log.Warn("[CTF] No 'PADSPAWN' NPC found in the session zones, no flag created.");

            _nextTickAt = GameLoop.GameLoopTime + TICK_INTERVAL;
        }

        protected override void OnClose()
        {
            foreach (FlagTracker t in _flags)
            {
                if (t.HasPin) Ctx.MapPins.Release(t.Pin);
                t.WorldObject?.RemoveFromWorld();
                t.Pad.RemoveFlag();
                t.Pad.RemoveFromWorld();
            }
            _flags.Clear();
            _byId.Clear();
        }

        public override void OnPlayerLeft(GamePlayer player) => ClearCarrier(player);

        public override void OnPlayerLinkdead(GamePlayer player)
        {
            for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
                if (player.Inventory.GetItem(slot) is FlagInventoryItem flag)
                    flag.DropFlagOnGround(player, null);
        }

        public void OnFlagSpawned(GameFlagStatic flag)
        {
            if (!IsActive || flag == null) return;
            if (!_byId.TryGetValue(flag.FlagID, out FlagTracker t))
            {
                log.Warn($"[CTF] Untracked flag object '{flag.Name}' (FlagID={flag.FlagID}) entered the world at {flag.Position}.");
                return;
            }
            t.WorldObject = flag;
            t.Carrier = null;
            t.MissingSince = 0;
        }

        public void OnFlagRemoved(GameFlagStatic flag)
        {
            if (!IsActive || flag == null) return;
            if (_byId.TryGetValue(flag.FlagID, out FlagTracker t) && t.WorldObject == flag)
                t.WorldObject = null;
        }

        public void OnFlagPickedUp(GamePlayer player, int flagId)
        {
            if (!IsActive || player == null) return;
            if (!_byId.TryGetValue(flagId, out FlagTracker t))
            {
                log.Warn($"[CTF] {player.Name} picked up an untracked flag (FlagID={flagId}).");
                return;
            }
            ClearCarrier(player);
            t.Carrier = player;
            t.WorldObject = null;
            t.MissingSince = 0;
        }

        public void OnFlagDropped(GamePlayer player, int flagId)
        {
            if (!IsActive || player == null) return;
            if (_byId.TryGetValue(flagId, out FlagTracker t) && t.Carrier == player)
                t.Carrier = null;
            else
                ClearCarrier(player);
        }

        private void ClearCarrier(GamePlayer player)
        {
            for (int i = 0; i < _flags.Count; i++)
                if (_flags[i].Carrier == player) _flags[i].Carrier = null;
        }

        #endregion

        #region tick

        public override int Tick(long now)
        {
            if (now < _nextTickAt) return Until(_nextTickAt, now);
            _nextTickAt = now + TICK_INTERVAL;
            if (_flags.Count == 0) return TICK_INTERVAL;

            IReadOnlyList<GamePlayer> players = Ctx.Participants.Players;
            bool transition = false;

            for (int i = 0; i < _flags.Count; i++)
            {
                FlagTracker t = _flags[i];
                FlagState state = Resolve(t, players, now, out Position pos, out bool visible);

                if (state != t.State)
                {
                    transition = true;
                    if (log.IsDebugEnabled) log.Debug($"[CTF] flag #{t.FlagID}: {t.State} -> {state} @ {pos}");
                    t.State = state;
                }

                if (!t.HasPin) continue;

                string nameKey = state switch
                {
                    FlagState.AtBase => "PvPManager.Journal.CTF.AtBase.Name",
                    FlagState.Carried => "PvPManager.Journal.CTF.Carried.Name",
                    FlagState.OnGround => "PvPManager.Journal.CTF.OnGround.Name",
                    FlagState.OnTempPad => "PvPManager.Journal.CTF.OnTempPad.Name",
                    _ => "PvPManager.Journal.CTF.AtBase.Name"
                };

                string descKey = state switch
                {
                    FlagState.AtBase => "PvPManager.Journal.CTF.AtBase.Desc",
                    FlagState.Carried => "PvPManager.Journal.CTF.Carried.Desc",
                    FlagState.OnGround => "PvPManager.Journal.CTF.OnGround.Desc",
                    FlagState.OnTempPad => "PvPManager.Journal.CTF.OnTempPad.Desc",
                    _ => "PvPManager.Journal.CTF.AtBase.Desc"
                };

                if (state == FlagState.Missing)
                    Ctx.MapPins.Hide(t.Pin);
                else if (state == FlagState.Carried)
                {
                    GamePlayer carrier = t.Carrier;
                    if (t.CarrierFilter == null || !ReferenceEquals(t.CarrierFilterOwner, carrier))
                    {
                        t.CarrierFilterOwner = carrier;
                        t.CarrierFilter = p => p != carrier;
                    }
                    Ctx.MapPins.Set(t.Pin, pos, visible, t.CarrierFilter, nameKey, descKey);
                }
                else
                    Ctx.MapPins.Set(t.Pin, pos, visible, null, nameKey, descKey);
            }

            if (transition) Ctx.MapPins.Refresh();
            return TICK_INTERVAL;
        }

        private FlagState Resolve(FlagTracker t, IReadOnlyList<GamePlayer> players, long now, out Position pos, out bool visible)
        {
            GamePlayer carrier = t.Carrier;
            if (carrier != null)
            {
                if (carrier.ObjectState == GameObject.eObjectState.Active && carrier.IsAlive && carrier.IsInPvP
                    && HasFlagInInventory(carrier, t.FlagID))
                {
                    pos = carrier.Position;
                    visible = true;
                    t.MissingSince = 0;
                    return FlagState.Carried;
                }
                t.Carrier = null;
            }

            GameFlagStatic flag = t.WorldObject;
            if (!IsLive(flag))
            {
                flag = t.Pad.CurrentFlag;
                t.WorldObject = IsLive(flag) ? flag : null;
                flag = t.WorldObject;
            }

            if (flag != null)
            {
                t.MissingSince = 0;
                pos = flag.Position;

                if (flag.CurrentTempPad != null) { visible = true; return FlagState.OnTempPad; }
                if (flag.IsDroppedOnGround) { visible = true; return FlagState.OnGround; }

                visible = AnyParticipantNear(flag, players);
                return FlagState.AtBase;
            }

            pos = Position.Nowhere;
            visible = false;

            if (t.MissingSince == 0)
                t.MissingSince = now;
            else if (now - t.MissingSince >= MISSING_GRACE_MS)
            {
                log.Warn($"[CTF] flag #{t.FlagID} has been missing for {MISSING_GRACE_MS} ms, respawning it on its base pad.");
                t.MissingSince = 0;
                t.Pad.RespawnFlag();
            }
            return FlagState.Missing;
        }

        private static bool IsLive(GameObject obj) => obj != null && obj.ObjectState == GameObject.eObjectState.Active;

        private static bool HasFlagInInventory(GamePlayer p, int flagId)
        {
            for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
                if (p.Inventory.GetItem(slot) is FlagInventoryItem f && f.FlagID == flagId) return true;
            return false;
        }

        private static bool AnyParticipantNear(GameStaticItem flag, IReadOnlyList<GamePlayer> players)
        {
            for (int i = 0; i < players.Count; i++)
                if (players[i].IsWithinRadius(flag, BASE_PAD_PROXIMITY)) return true;
            return false;
        }

        #endregion
    }
}