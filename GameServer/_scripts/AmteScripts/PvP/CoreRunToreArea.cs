using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;

namespace AmteScripts.PvP.CoreRun
{
    public class CoreRunTore : Area.Tore
    {
        private int _greenModel;
        private int _redModel;
        private int _greenSpellID;
        private int _redSpellID;
        public int MaxRadius => m_MaxRadius;

        public CoreRunTore(string name, Coordinate center, int innerRadius, int outerRadius,
                           int greenModel, int redModel, int greenSpell, int redSpell,
                           int effectAmount, int effectFreq, byte stormLevel, byte stormSize, int effectVariance,
                           Region region)
        {
            this.DbArea = new DBArea
            {
                Description = name,
                X = center.X,
                Y = center.Y,
                Z = center.Z,
                Radius = innerRadius,
                MaxRadius = outerRadius,
                BoundarySpacing = 300,
                SpellID = greenSpell,
                EffectAmount = effectAmount,
                EffectFrequency = effectFreq,
                StormLevel = stormLevel,
                StormSize = stormSize,
                EffectVariance = effectVariance,
                IsPvP = true
            };

            this.LoadFromDatabase(this.DbArea);
            this.Region = region;

            _greenModel = greenModel;
            _redModel = redModel;
            _greenSpellID = greenSpell;
            _redSpellID = redSpell;
        }

        public void UpdateVisuals(bool isRedLight)
        {
            ClearBoundary();
            int model = isRedLight ? _redModel : _greenModel;

            SpawnManualCircle(m_Radius, true, model, 300);
            SpawnManualCircle(m_MaxRadius, false, model, 50);
            this.DbArea.SpellID = isRedLight ? _redSpellID : _greenSpellID;
        }

        private void SpawnManualCircle(int radius, bool inward, int model, int spacing)
        {
            if (radius <= 0) return;

            double perimeter = 2.0 * Math.PI * radius;
            int count = (int)Math.Round(perimeter / spacing);
            count = Math.Max(6, Math.Min(256, count));

            for (int i = 0; i < count; i++)
            {
                double t = (2.0 * Math.PI * i) / count;
                Angle angle = Angle.Radians(t);
                Vector offset = Vector.Create(angle, radius);

                ushort heading = inward ? Angle.Radians(t + Math.PI).InHeading : angle.InHeading;

                var pos = Position.Create(Region.ID, Coordinate.X + offset.X, Coordinate.Y + offset.Y, Coordinate.Z, heading);

                if (Region.GetZone(pos.Coordinate) != null)
                {
                    var marker = new GameStaticItem();
                    marker.Model = (ushort)model;
                    marker.Name = "Core Run Boundary";
                    marker.Position = pos;
                    marker.AddToWorld();

                    m_boundaryObjects.Add(marker);
                }
            }
        }
    }
}