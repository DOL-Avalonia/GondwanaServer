using System;
using System.Collections.Generic;
using System.Linq;
using AmteScripts.PvP.CTF;
using DOL.GS;
using DOL.GS.Geometry;

namespace AmteScripts.PvP.KotH
{
    public class KotHBanner : GamePvPStaticItem
    {
        private int _radius;
        public List<GameStaticItem> MiniBanners { get; } = new List<GameStaticItem>();

        private const ushort MODEL_NEUTRAL = 2551;
        private const ushort MODEL_OWNED = 679;
        private const ushort MODEL_BOUNDARY = 3223;

        public Guild OwningGuild { get; private set; }
        public GamePlayer OwningSolo { get; private set; }

        public KotHBanner() : base() { }

        public void Setup(int radius, int x, int y, int z, int regionId)
        {
            _radius = radius;
            Name = "King of the Hill";
            Model = MODEL_NEUTRAL;

            Position = Position.Create((ushort)regionId, x, y, z, 0);

            AddToWorld();
            SpawnBorders();
        }

        private void SpawnBorders()
        {
            ClearBorders();

            if (_radius <= 0) return;

            double k = 10.0 / (2.0 * Math.PI * 1000.0);
            int count = (int)Math.Round((2.0 * Math.PI * _radius) * k);
            count = Math.Max(6, Math.Min(64, count));
            int ringRadius = Math.Max(64, _radius - 50);

            var center = this.Position;

            for (int i = 0; i < count; i++)
            {
                double t = (2.0 * Math.PI * i) / count;
                Angle outward = Angle.Radians(t);
                Vector offset = Vector.Create(outward, ringRadius);

                var mini = new GameStaticItem();
                mini.Model = MODEL_BOUNDARY;
                mini.Name = "Boundary";

                mini.Emblem = this.Emblem;

                mini.Position = Position.Create(
                    regionID: CurrentRegionID,
                    x: center.X + offset.X,
                    y: center.Y + offset.Y,
                    z: center.Z,
                    heading: outward.InHeading
                );

                mini.AddToWorld();
                MiniBanners.Add(mini);
            }
        }

        public void SetOwner(Guild guild, GamePlayer soloPlayer)
        {
            OwningGuild = guild;
            OwningSolo = soloPlayer;

            if (guild != null)
            {
                Name = $"Hill Controlled by {guild.Name}";
                Model = MODEL_OWNED;

                base.SetOwnership(null, false);
                this.OwnerGuild = guild;
                this.Emblem = guild.Emblem;
            }
            else if (soloPlayer != null)
            {
                Name = $"Hill Controlled by {soloPlayer.Name}";
                Model = MODEL_OWNED;
                base.SetOwnership(soloPlayer, true);
            }
            else
            {
                Name = "King of the Hill (Neutral)";
                Model = MODEL_NEUTRAL;
                base.SetOwnership(null, true);
            }

            foreach (GamePlayer p in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).OfType<GamePlayer>())
            {
                p.Out.SendObjectUpdate(this);
            }

            foreach (var mini in MiniBanners)
            {
                if (mini.Emblem != this.Emblem)
                {
                    mini.Emblem = this.Emblem;
                    foreach (var p in mini.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).OfType<GamePlayer>())
                    {
                        p.Out.SendObjectUpdate(mini);
                    }
                }
            }
        }

        private void ClearBorders()
        {
            foreach (var b in MiniBanners) b.RemoveFromWorld();
            MiniBanners.Clear();
        }

        public override void Delete()
        {
            ClearBorders();
            RemoveFromWorld();
            base.Delete();
        }
    }
}