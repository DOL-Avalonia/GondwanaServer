using AmteScripts.PvP.KotH;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.GS.Spells;
using DOL.Language;
using System;
using System.Collections.Generic;
using static AmteScripts.Managers.PvpManager;

namespace AmteScripts.PvP.Modes
{
    public sealed class KingOfTheHillMode : PvPGameModeBase
    {
        public override eSessionTypes Type => eSessionTypes.KingOfTheHill;
        public override int KillBasePoints => 5;
        public override bool KillHasRr5Bonus => true;
        private const int TICK_RATE = 8000;
        private const int SOLO_SCORE_MULTIPLIER = 2;
        private const int NEUTRAL_CAPTURE_POINTS = 50;
        private const int STEAL_CAPTURE_MAX_POINTS = 30;
        private const int FULL_HOLD_MS = 300_000;

        private KotHBanner _hill;
        private byte _pin;
        private bool _hasPin;
        private readonly List<Spawn> _potential = new();
        private readonly Queue<Spawn> _rotation = new();
        private readonly Dictionary<object, double> _pressure = new();
        private long _nextTickAt, _nextMoveAt, _ownershipStartAt;

        private int Radius => Ctx.TempAreaRadius > 0 ? Ctx.TempAreaRadius : 1000;
        private object CurrentOwner => _hill == null ? null : (object)_hill.OwningGuild ?? _hill.OwningSolo;

        protected override void OnOpen()
        {
            _hasPin = Ctx.MapPins.TryAllocate(out _pin);

            foreach (GameNPC n in Ctx.FindNpcs(static n => n.Realm == eRealm.None && n.GuildName == "PVP" && n.Name.IndexOf("KOTH_HILL", StringComparison.OrdinalIgnoreCase) >= 0))
                _potential.Add(new Spawn(n, n.Position));

            if (_potential.Count == 0)
            {
                log.Warn("[KotH] No 'KOTH_HILL' NPCs found, falling back to the player SPAWN NPCs.");
                foreach (GameNPC n in Ctx.SpawnNpcs) _potential.Add(new Spawn(n, n.Position));
            }
            if (_potential.Count == 0)
            {
                log.Error("[KotH] No hill location available at all; the hill will never spawn.");
                return;
            }

            RefillRotation();
            SpawnNextHill();
            _nextTickAt = GameLoop.GameLoopTime + TICK_RATE;
        }

        protected override void OnClose()
        {
            if (_hasPin) { Ctx.MapPins.Release(_pin); _hasPin = false; }
            if (_hill != null) { _hill.Delete(); _hill = null; }
            _potential.Clear();
            _rotation.Clear();
            _pressure.Clear();
        }

        public override int Tick(long now)
        {
            if (now < _nextTickAt) return Until(_nextTickAt, now);
            _nextTickAt = now + TICK_RATE;

            if (_hill == null) { SpawnNextHill(); return TICK_RATE; }
            if (now >= _nextMoveAt) SpawnNextHill();
            else { ProcessHillOwnership(); UpdatePin(); }
            return TICK_RATE;
        }

        /// <summary>
        /// Visible only once the current hill has been captured; a rotation hides it again.
        /// </summary>
        private void UpdatePin()
        {
            if (!_hasPin) return;
            Ctx.MapPins.Set(_pin, _hill?.Position ?? Position.Nowhere, _hill != null && CurrentOwner != null);
        }

        private void RefillRotation()
        {
            _rotation.Clear();
            if (_potential.Count == 0) return;
            Spawn[] arr = _potential.ToArray();
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = Util.Random(i);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            foreach (Spawn s in arr) _rotation.Enqueue(s);
        }

        private void SpawnNextHill()
        {
            if (_hill != null) { _hill.Delete(); _hill = null; }
            if (_rotation.Count == 0) RefillRotation();
            if (_rotation.Count == 0) return;

            Spawn next = _rotation.Dequeue();
            _hill = new KotHBanner();
            _hill.Setup(Radius, next.Position.X, next.Position.Y, next.Position.Z, next.Position.RegionID);

            int duration = Properties.KOTH_DURATION_SECONDS * 1000;
            if (duration <= 0) duration = 300000;
            _nextMoveAt = GameLoop.GameLoopTime + duration;
            _ownershipStartAt = 0;

            UpdatePin();
            Ctx.MapPins.Refresh();
            Ctx.Broadcast("PvPManager.KingHill.HillMoved", eChatType.CT_ScreenCenter);
        }

        #region ownership

        private static bool IsEffectivelyAlive(GamePlayer p) =>
            p.IsInPvP && (p.IsAlive || p.IsDamned || SpellHandler.FindEffectOnTarget(p, "SummonMonster") != null);

        private static double Pressure(GameLiving living)
        {
            if (living is GamePlayer p)
            {
                if (!IsEffectivelyAlive(p)) return 0;
                double pts = 1.0;
                if (p.RealmLevel >= 40) pts += 0.4;
                if (p.IsDamned) pts += 0.4;
                if (SpellHandler.FindEffectOnTarget(p, "SummonMonster") != null) pts += 0.4;
                return pts;
            }
            if (living is GamePet pet)
            {
                GamePlayer owner = pet.GetPlayerOwner();
                if (owner == null || !IsEffectivelyAlive(owner)) return 0;
                return pet switch { TheurgistPet => 0.0, TurretPet => 0.25, BDSubPet => 0.10, CommanderPet => 0.30, _ => 0.5 };
            }
            return 0;
        }

        private static object Faction(GameLiving living)
        {
            GamePlayer p = living as GamePlayer ?? (living as GamePet)?.GetPlayerOwner();
            if (p == null) return null;
            return PvpService.Instance.AllowsGroups && p.Guild != null ? p.Guild : p;
        }

        private void ProcessHillOwnership()
        {
            _pressure.Clear();
            ushort r = (ushort)Radius;
            foreach (GamePlayer p in _hill.GetPlayersInRadius(r))
            {
                object f = Faction(p);
                if (f != null) _pressure[f] = _pressure.GetValueOrDefault(f) + Pressure(p);
            }
            foreach (GameNPC n in _hill.GetNPCsInRadius(r))
            {
                if (n is not GamePet pet) continue;
                object f = Faction(pet);
                if (f != null) _pressure[f] = _pressure.GetValueOrDefault(f) + Pressure(pet);
            }

            object owner = CurrentOwner;
            object dominant = null; double highest = 0;
            foreach (KeyValuePair<object, double> kv in _pressure)
                if (kv.Value > highest) { highest = kv.Value; dominant = kv.Key; }

            if (owner == null)
            {
                if (dominant != null && highest > 0) Capture(dominant, true);
                return;
            }

            double ownerP = _pressure.GetValueOrDefault(owner);
            object enemy = null; double enemyP = 0;
            foreach (KeyValuePair<object, double> kv in _pressure)
                if (kv.Key != owner && kv.Value > enemyP) { enemyP = kv.Value; enemy = kv.Key; }

            if (enemyP > ownerP) { Capture(enemy, false); return; }
            if (ownerP <= 0) return;

            if (enemyP == 0 || ownerP / (ownerP + enemyP) > 0.60) AwardHold(owner, 2);
            else { AwardHold(owner, 1); AwardPressure(enemy, 1); }
        }

        private void Capture(object faction, bool neutral)
        {
            Guild guild = faction as Guild;
            GamePlayer solo = faction as GamePlayer;
            int points = NEUTRAL_CAPTURE_POINTS;
            if (!neutral)
            {
                double pct = Math.Min(1.0, (GameLoop.GameLoopTime - _ownershipStartAt) / (double)FULL_HOLD_MS);
                points = Math.Max(1, (int)(STEAL_CAPTURE_MAX_POINTS * pct));
            }
            _hill.SetOwner(guild, solo);
            _ownershipStartAt = GameLoop.GameLoopTime;
            AwardCapture(faction, points);
            UpdatePin();
            Ctx.MapPins.Refresh();

            string name = guild != null ? guild.Name : solo!.Name;
            foreach (GamePlayer p in _hill.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                p.Out.SendSpellEffectAnimation(_hill, _hill, 5811, 0, false, 1);
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client.Account.Language, "PvPManager.KingHill.CapturedBy", name), eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
            }
        }

        private void AwardToFaction(object faction, Action<PvPScore> guildFun, Action<PvPScore> soloFun, string msgKey, eChatType type, object[] guildArgs, object[] soloArgs)
        {
            if (faction is Guild g)
            {
                Ctx.Scoreboard.Award(g, guildFun);
                foreach (GamePlayer m in g.GetListOfOnlineMembers())
                    if (m.IsInPvP) m.Out.SendMessage(LanguageMgr.GetTranslation(m.Client.Account.Language, msgKey, guildArgs), type, eChatLoc.CL_SystemWindow);
            }
            else if (faction is GamePlayer s)
            {
                Ctx.Scoreboard.Award(s, soloFun);
                s.Out.SendMessage(LanguageMgr.GetTranslation(s.Client.Account.Language, msgKey, soloArgs), type, eChatLoc.CL_SystemWindow);
            }
        }

        private void AwardPressure(object f, int a) => AwardToFaction(f,
            s => s.KotH_PressurePoints += a, s => s.KotH_PressurePoints += a * 2,
            "PvPManager.KingHill.Pressuring", eChatType.CT_System, new object[] { a }, new object[] { a * 2 });

        private void AwardHold(object f, int a) => AwardToFaction(f,
            s => { s.KotH_Points += a; s.KotH_Ticks++; }, s => { s.KotH_Points += a * 2; s.KotH_Ticks++; },
            "PvPManager.KingHill.Holding", eChatType.CT_System, new object[] { a }, new object[] { a * 2 });

        private void AwardCapture(object f, int a) => AwardToFaction(f,
            s => { s.KotH_CapturePoints += a; s.KotH_Captures++; }, s => { s.KotH_CapturePoints += a * SOLO_SCORE_MULTIPLIER; s.KotH_Captures++; },
            "PvPManager.KingHill.YouCapture", eChatType.CT_Spell, Array.Empty<object>(), Array.Empty<object>());

        #endregion
    }
}