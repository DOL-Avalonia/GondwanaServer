using System.Linq;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.Territories;
using System.Collections.Generic;
using static AmteScripts.Managers.PvpManager;

namespace AmteScripts.PvP.Modes
{
    public sealed class TerritoryCaptureMode : PvPGameModeBase
    {
        public override eSessionTypes Type => eSessionTypes.TerritoryCapture;

        private const int OWNERSHIP_INTERVAL = 30_000;
        private long _nextAwardAt;
        private readonly Dictionary<Territory, byte> _terrPins = new();

        protected override void OnOpen()
        {
            _nextAwardAt = GameLoop.GameLoopTime + 10_000;
        }

        protected override void OnClose()
        {
            TerritoryManager.Instance.ReleaseSubTerritoriesInZones(Ctx.Zones);
            foreach (byte pin in _terrPins.Values) Ctx.MapPins.Release(pin);
            _terrPins.Clear();
        }

        public override List<GameStaticItem> CreateOutpostItems(Position center, GamePlayer owner) => PvPAreaOutposts.CreateGuildOutpostTemplate01(center, owner);

        public override int Tick(long now)
        {
            UpdatePins();

            if (now < _nextAwardAt) return Until(_nextAwardAt, now);
            _nextAwardAt = now + OWNERSHIP_INTERVAL;

            foreach (Territory t in TerritoryManager.Instance.Territories)
                if (t.Type == Territory.eType.Subterritory && t.OwnerGuild != null && t.Zone != null && Ctx.IsSessionZone(t.Zone))
                    Ctx.Scoreboard.Award(t.OwnerGuild, static s => s.Terr_TerritoriesOwnershipPoints++);

            return 2000;
        }

        public void AwardCapturePoints(Territory t, GamePlayer p)
        {
            if (!IsActive || t.Type != Territory.eType.Subterritory || t.Zone == null || !Ctx.IsSessionZone(t.Zone)) return;
            Ctx.Scoreboard.Award(p, static s => { s.Terr_TerritoriesCapturedPoints += 20; s.Terr_TerritoriesCapturedCount++; });
        }

        public void UpdateAllTerritoryMarkers(GamePlayer specific = null)
        {
            if (!IsActive) return;

            UpdatePins();

            if (specific != null)
                Ctx.MapPins.SendAll(specific);
            else
                Ctx.MapPins.Refresh();
        }

        private void UpdatePins()
        {
            if (!IsActive) return;
            var active = TerritoryManager.Instance.Territories.Where(t => t.Type == Territory.eType.Subterritory && t.Zone != null && Ctx.IsSessionZone(t.Zone)).ToList();

            foreach (Territory t in active)
            {
                if (t.Boss == null) continue;

                if (!_terrPins.TryGetValue(t, out byte pin))
                {
                    if (Ctx.MapPins.TryAllocate(out pin))
                        _terrPins[t] = pin;
                }

                if (_terrPins.TryGetValue(t, out pin))
                {

                    bool isVisible = !t.IsNeutral();
                    Ctx.MapPins.Set(pin, t.Boss.Position, isVisible);
                }
            }
        }
    }
}