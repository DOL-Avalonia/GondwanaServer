using System;
using System.Collections.Generic;
using System.Linq;
using AmteScripts.Managers;
using AmteScripts.PvP.Modes;
using DOL.Database;
using DOL.Events;
using DOL.GameEvents;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;

namespace AmteScripts.PvP.CoreRun
{
    /// "Run to the Core". Ticked by PvpService on the ModuleService context (single-threaded).
    /// Replaces _coreRunCycleTimer / _coreRunMovementTimer / _coreRunDecayTimer and CoreRunTore.EffectLoopCallback.
    public sealed class CoreRunMode : PvPGameModeBase
    {
        public override PvpManager.eSessionTypes Type => PvpManager.eSessionTypes.CoreRun;
        public override int KillBasePoints => 5;
        public override bool KillHasRr5Bonus => true;

        public const string CONTROLLER_NPC_NAME = "CoreRunEventController";
        private const string PROP_STATE = "CoreRunState";
        private const int MOVEMENT_TOLERANCE = 50, ROTATION_TOLERANCE = 300;
        private const int MOVEMENT_CHECK_INTERVAL = 500, DECAY_INTERVAL = 5200, FIRST_CYCLE_DELAY = 10_000;
        private const int MODEL_GREEN_LIGHT = 3496, MODEL_RED_LIGHT = 3498;
        private const int SPELL_GREEN_LIGHT = 25318, SPELL_RED_LIGHT = 25317;
        private const int EFFECT_AMOUNT = 1, EFFECT_FREQ = 2200, EFFECT_VARIANCE = 20;
        private const byte STORM_LEVEL = 60, STORM_SIZE = 80;
        private const int STORM_SPAWN_RADIUS = 900, STORM_CENTER_EXCLUSION = 390, STORM_SPOT_ATTEMPTS = 5;
        private const int ANCHOR_RETRY_MS = 3000, ANCHOR_MAX_RETRIES = 12;
        private int _anchorRetries;
        private long _nextAnchorRetryAt;

        private GameNPC _anchor;
        private CoreRunTore _tore;
        private Area.Circle _safeZone;
        private bool _redLight;
        private long _nextCycleAt, _nextDecayAt, _nextEffectAt, _nextMovementCheckAt = long.MaxValue;

        private struct Snapshot { public Coordinate Coord; public ushort Heading; }
        private readonly Dictionary<GamePlayer, Snapshot> _snapshots = new();       // by reference: no GetClientByPlayerID scans
        private readonly List<GamePlayer> _scratch = new(64), _violators = new(8), _effectCenters = new(32);
        private readonly List<InventoryItem> _updatedItems = new(4);
        private GameNPC _executioner;                                              // reused "Red Light" killer
        private readonly DOLEventHandler _onCast, _onAttack, _onAreaEnter, _onAreaLeave;

        public CoreRunMode()
        {
            _onCast = OnPlayerCast; _onAttack = OnPlayerAttack; _onAreaEnter = OnAreaEnter; _onAreaLeave = OnAreaLeave;
        }

        #region lifecycle

        protected override void OnOpen()
        {
            if (!TryInitialize()) _nextAnchorRetryAt = GameLoop.GameLoopTime + ANCHOR_RETRY_MS;
        }

        private bool TryInitialize()
        {
            _anchor = Ctx.FindNpc(static n => n.Name.Equals(CONTROLLER_NPC_NAME, StringComparison.OrdinalIgnoreCase));

            if (_anchor == null)
            {
                if (_anchorRetries >= ANCHOR_MAX_RETRIES) log.Error($"[CoreRun] NPC '{CONTROLLER_NPC_NAME}' not found in zones [{string.Join(",", Ctx.Zones.Select(z => z.ID))}]. Mode disabled.");
                else log.Warn($"[CoreRun] NPC '{CONTROLLER_NPC_NAME}' not found yet, retry {_anchorRetries + 1}/{ANCHOR_MAX_RETRIES} in {ANCHOR_RETRY_MS / 1000}s.");
                return false;
            }

            Region region = _anchor.CurrentRegion;
            int inner = Ctx.TempAreaRadius > 0 ? Ctx.TempAreaRadius : 1200;
            _tore = new CoreRunTore("Core Run Danger Zone", _anchor.Coordinate, inner, inner * 14, MODEL_GREEN_LIGHT, MODEL_RED_LIGHT,
                                    SPELL_GREEN_LIGHT, SPELL_RED_LIGHT, EFFECT_AMOUNT, EFFECT_FREQ, STORM_LEVEL, STORM_SIZE, EFFECT_VARIANCE, region);
            region.AddArea(_tore);
            _tore.BuildBoundary();

            _safeZone = new Area.Circle();
            _safeZone.LoadFromDatabase(new DBArea
            {
                Description = "Core Run Safe Zone",
                X = _anchor.Coordinate.X,
                Y = _anchor.Coordinate.Y,
                Z = _anchor.Coordinate.Z,
                Radius = inner,
                SafeArea = true,
                IsPvP = false
            });
            _safeZone.Region = region;
            region.AddArea(_safeZone);

            _tore.RegisterPlayerEnter(_onAreaEnter);
            _tore.RegisterPlayerLeave(_onAreaLeave);
            _safeZone.RegisterPlayerEnter(_onAreaEnter);
            foreach (GamePlayer p in Ctx.Participants.Players) OnPlayerJoined(p);

            _redLight = false;
            _tore.SetLight(false);
            long now = GameLoop.GameLoopTime;
            _nextCycleAt = now + FIRST_CYCLE_DELAY;
            _nextDecayAt = now + DECAY_INTERVAL;
            _nextEffectAt = now + EFFECT_FREQ;
            _nextMovementCheckAt = long.MaxValue;
            Ctx.Broadcast("PvPManager.CoreRun.Begins", eChatType.CT_ScreenCenter);

            return true;
        }

        protected override void OnClose()
        {
            foreach (GamePlayer p in Ctx.Participants.Players) OnPlayerLeft(p);
            _snapshots.Clear();
            if (_tore != null)
            {
                _tore.UnRegisterPlayerEnter(_onAreaEnter);
                _tore.UnRegisterPlayerLeave(_onAreaLeave);
                _tore.ClearBoundary();
                _tore.Region?.RemoveArea(_tore);
                _tore = null;
            }
            if (_safeZone != null)
            {
                _safeZone.UnRegisterPlayerEnter(_onAreaEnter);
                _safeZone.Region?.RemoveArea(_safeZone);
                _safeZone = null;
            }
            _anchor = null;
            _redLight = false;
        }

        public override void OnPlayerJoined(GamePlayer player)
        {
            GameEventMgr.AddHandlerUnique(player, GameLivingEvent.CastStarting, _onCast);
            GameEventMgr.AddHandlerUnique(player, GameLivingEvent.AttackStarted, _onAttack);
        }

        public override void OnPlayerLeft(GamePlayer player)
        {
            GameEventMgr.RemoveHandler(player, GameLivingEvent.CastStarting, _onCast);
            GameEventMgr.RemoveHandler(player, GameLivingEvent.AttackStarted, _onAttack);
            _snapshots.Remove(player);
            player.TempProperties.removeProperty(PROP_STATE);
        }

        #endregion

        #region tick

        public override int Tick(long now)
        {
            if (_tore == null)
            {
                if (_anchorRetries < ANCHOR_MAX_RETRIES && now >= _nextAnchorRetryAt)
                {
                    _anchorRetries++;
                    if (!TryInitialize()) _nextAnchorRetryAt = now + ANCHOR_RETRY_MS;
                }
                return ANCHOR_RETRY_MS;
            }

            if (_tore == null) return 30_000;
            if (now >= _nextCycleAt) ToggleLight(now);
            if (_redLight && now >= _nextMovementCheckAt) { CheckMovement(); _nextMovementCheckAt = now + MOVEMENT_CHECK_INTERVAL; }
            if (now >= _nextDecayAt) { DecayCores(); _nextDecayAt = now + DECAY_INTERVAL; }
            if (now >= _nextEffectAt) { SpawnStormWave(); _nextEffectAt = now + NextEffectInterval(); }

            long next = Math.Min(_nextCycleAt, Math.Min(_nextDecayAt, _nextEffectAt));
            if (_redLight) next = Math.Min(next, _nextMovementCheckAt);
            return Until(next, now);
        }

        private void ToggleLight(long now)
        {
            _redLight = !_redLight;
            _tore.SetLight(_redLight);
            if (_redLight)
            {
                BroadcastDanger("PvPManager.CoreRun.RedLight", eChatType.CT_ScreenCenter);
                TakeSnapshots();
                _nextMovementCheckAt = now + MOVEMENT_CHECK_INTERVAL;
                _nextCycleAt = now + Util.Random(4500, 8000);
            }
            else
            {
                BroadcastDanger("PvPManager.CoreRun.GreenLight", eChatType.CT_ScreenCenter);
                BroadcastDanger("PvPManager.CoreRun.GreenLight", eChatType.CT_System);
                _snapshots.Clear();
                _nextMovementCheckAt = long.MaxValue;
                _nextCycleAt = now + Util.Random(6000, 12000);
            }
        }

        private bool IsInDangerZone(GamePlayer p) => _tore.IsContaining(p.Coordinate) && !_safeZone.IsContaining(p.Coordinate);

        private void BroadcastDanger(string key, eChatType type)
        {
            foreach (GamePlayer p in Ctx.Participants.Players)
                if (p.IsAlive && IsInDangerZone(p))
                    p.Out.SendMessage(LanguageMgr.GetTranslation(p.Language, key), type, eChatLoc.CL_SystemWindow);
        }

        private void TakeSnapshots()
        {
            _snapshots.Clear();
            foreach (GamePlayer p in Ctx.Participants.Players)
                if (p.IsAlive && IsInDangerZone(p))
                    _snapshots[p] = new Snapshot { Coord = p.Coordinate, Heading = p.Heading };
        }

        private void CheckMovement()
        {
            _violators.Clear();
            foreach (KeyValuePair<GamePlayer, Snapshot> kv in _snapshots)
            {
                GamePlayer p = kv.Key;
                if (!p.IsAlive || !p.IsInPvP || p.ObjectState != GameObject.eObjectState.Active || !IsInDangerZone(p)) continue;
                bool moved = !p.Coordinate.IsWithinDistance(kv.Value.Coord, MOVEMENT_TOLERANCE);   // squared, no sqrt
                int diff = Math.Abs(p.Heading - kv.Value.Heading);
                if (diff > 2048) diff = 4096 - diff;
                if (moved || diff > ROTATION_TOLERANCE) _violators.Add(p);
            }
            foreach (GamePlayer p in _violators) { _snapshots.Remove(p); Eliminate(p); }
        }

        private void Eliminate(GamePlayer p)
        {
            if (!IsActive || !p.IsAlive) return;
            p.Out.SendMessage(LanguageMgr.GetTranslation(p.Language, "PvPManager.CoreRun.RedLightViolation"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            p.Out.SendSpellEffectAnimation(p, p, 7504, 0, false, 1);
            p.Out.SendSpellEffectAnimation(p, p, 13108, 0, false, 1);
            Ctx.Scoreboard.Award(p, static s => s.CoreRun_Eliminations++);
            _executioner ??= new GameNPC { Name = "Red Light", Realm = eRealm.Albion };
            p.Die(_executioner);
        }

        private void RefreshHeading(GamePlayer p)
        {
            if (_snapshots.TryGetValue(p, out Snapshot snap)) { snap.Heading = p.Heading; _snapshots[p] = snap; }
        }

        private void DecayCores()
        {
            foreach (GamePlayer p in Ctx.Participants.Players)
            {
                if (!p.IsAlive || !IsInDangerZone(p)) continue;
                _updatedItems.Clear();
                for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
                {
                    if (p.Inventory.GetItem(slot) is PvPTreasure { Condition: > 80 } core && IsCore(core))
                    {
                        core.Condition -= 1;
                        core.Dirty = true;
                        _updatedItems.Add(core);
                    }
                }
                if (_updatedItems.Count == 0) continue;
                p.Out.SendInventoryItemsUpdate(_updatedItems);
                p.Out.SendSpellEffectAnimation(p, p, 14326, 0, false, 1);
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Language, "PvPManager.CoreRun.CoreDestabilizing"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
            }
        }

        private static bool IsCore(InventoryItem item) => item.Id_nb != null && item.Id_nb.IndexOf("core", StringComparison.OrdinalIgnoreCase) >= 0;

        private static InventoryItem GetCoreItem(GamePlayer p)
        {
            for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
                if (p.Inventory.GetItem(slot) is PvPTreasure t && IsCore(t)) return t;
            return null;
        }

        #endregion

        #region storms (pooled via AbstractArea.SpawnStorm)

        private static int NextEffectInterval()
        {
            int varMs = (int)(EFFECT_FREQ * (EFFECT_VARIANCE / 100.0));
            return Math.Max(500, Util.Random(EFFECT_FREQ - varMs, EFFECT_FREQ + varMs));
        }

        private static int Vary(int value)
        {
            int range = (int)Math.Round(value * (EFFECT_VARIANCE / 100.0));
            return range <= 0 ? value : Math.Max(1, Util.Random(value - range, value + range));
        }

        private void SpawnStormWave()
        {
            int spellID = _tore.ActiveSpellID;
            if (spellID <= 0) return;
            _effectCenters.Clear();
            Ctx.Participants.Snapshot(_scratch);
            foreach (GamePlayer p in _scratch)
            {
                if (!p.IsAlive || !IsInDangerZone(p)) continue;
                bool tooClose = false;
                foreach (GamePlayer c in _effectCenters) if (p.IsWithinRadius(c, STORM_CENTER_EXCLUSION)) { tooClose = true; break; }
                if (tooClose) continue;

                int amount = Vary(EFFECT_AMOUNT);
                for (int i = 0; i < amount; i++)
                    if (TryPickStormSpot(p, out Coordinate spot))
                        AbstractArea.SpawnStorm(_tore.Region, spot, spellID, (byte)Math.Clamp(Vary(STORM_LEVEL), 1, 255), STORM_SIZE,
                                                _tore.DbArea.StormFaction, _tore.DbArea.NPCImmunToStorm);
                _effectCenters.Add(p);
            }
        }

        private bool TryPickStormSpot(GamePlayer center, out Coordinate spot)
        {
            for (int attempt = 0; attempt < STORM_SPOT_ATTEMPTS; attempt++)
            {
                double angle = Util.RandomDouble() * Math.PI * 2;
                double r = Math.Sqrt(Util.RandomDouble()) * STORM_SPAWN_RADIUS;
                spot = Coordinate.Create(center.Coordinate.X + (int)(r * Math.Cos(angle)), center.Coordinate.Y + (int)(r * Math.Sin(angle)), center.Coordinate.Z);
                if (!_tore.IsContaining(spot)) continue;                                    // retry only for containment (original)
                foreach (GamePlayer c in _effectCenters) if (spot.IsWithinDistance(c.Coordinate, STORM_SPAWN_RADIUS)) return false;
                return _tore.Region.GetZone(spot) != null;
            }
            spot = Coordinate.Nowhere;
            return false;
        }

        #endregion

        #region event handlers – may run on ANY thread: cheap reads here, mutations posted

        private void OnPlayerCast(DOLEvent e, object sender, EventArgs args)
        {
            CoreRunTore tore = _tore;
            if (!_redLight || tore == null || sender is not GamePlayer { IsInPvP: true } player) return;
            if (args is not CastingEventArgs { SpellHandler: { } handler } || !tore.IsContaining(player.Coordinate)) return;

            Spell spell = handler.Spell;
            if (spell.CastTime > 0 || !string.Equals(spell.SpellType, "BumpSpell", StringComparison.OrdinalIgnoreCase))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Language, "PvPManager.CoreRun.CastMovement"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                handler.InterruptCasting();                                                // immediate, same thread as before
                ModuleService.Run(static s => s.mode.Eliminate(s.player), (mode: this, player));
            }
            else
                ModuleService.Run(static s => s.mode.RefreshHeading(s.player), (mode: this, player));
        }

        private void OnPlayerAttack(DOLEvent e, object sender, EventArgs args)
        {
            CoreRunTore tore = _tore;
            if (!_redLight || tore == null || sender is not GamePlayer { IsInPvP: true } player || !tore.IsContaining(player.Coordinate)) return;
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Language, "PvPManager.CoreRun.AttackMovement"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            player.StopAttack();
            ModuleService.Run(static s => s.mode.Eliminate(s.player), (mode: this, player));
        }

        private void OnAreaEnter(DOLEvent e, object sender, EventArgs args)
        {
            if (args is not AreaEventArgs { GameObject: GamePlayer player } a) return;
            ModuleService.Run(static s => s.mode.HandleAreaEnter(s.player, s.area), (mode: this, player, area: a.Area));
        }

        private void OnAreaLeave(DOLEvent e, object sender, EventArgs args)
        {
            if (args is not AreaEventArgs { GameObject: GamePlayer player }) return;
            ModuleService.Run(static s => s.mode.HandleAreaLeave(s.player), (mode: this, player));
        }

        private void HandleAreaEnter(GamePlayer player, IArea area)
        {
            if (!IsActive || !player.IsInPvP) return;
            if (area == _safeZone)
            {
                if (player.TempProperties.getProperty<int>(PROP_STATE, 0) != 0) return;
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Language, "PvPManager.CoreRun.ReachedCore"), eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                Ctx.Scoreboard.Award(player, static s => s.CoreRun_CenterReached++);
                player.TempProperties.setProperty(PROP_STATE, 1);
                player.BindPosition = Position.Create(_safeZone.Region.ID, _safeZone.Coordinate.X, _safeZone.Coordinate.Y, _safeZone.Coordinate.Z, 0);
                player.Out.SendSpellEffectAnimation(player, player, 106, 0, false, 1);
            }
            else if (area == _tore && _redLight)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Language, "PvPManager.CoreRun.EnteredRedLight"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                Eliminate(player);
            }
        }

        /// Was never registered before (StopCoreRun even ADDED a PlayerLeave handler instead of removing one).
        /// Remove the RegisterPlayerLeave call in OnOpen if the "1 point per completed run" was not intended.
        private void HandleAreaLeave(GamePlayer player)
        {
            if (!IsActive || !player.IsInPvP || _safeZone == null || _tore == null) return;
            if (player.Coordinate.DistanceTo(_safeZone.Coordinate) < _tore.MaxRadius) return;    // left inwards
            if (player.TempProperties.getProperty<int>(PROP_STATE, 0) != 1) return;
            if (GetCoreItem(player) != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Language, "PvPManager.CoreRun.RunComplete"), eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                PvpManager.Instance.HandleCoreDelivery(player, 1);
            }
            else
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Language, "PvPManager.CoreRun.LeftWithoutCore"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        #endregion
    }
}