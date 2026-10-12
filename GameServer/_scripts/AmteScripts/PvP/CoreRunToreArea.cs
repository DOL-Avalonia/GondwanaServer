using DOL.Database;
using DOL.GS;
using DOL.GS.Geometry;
using System;

namespace AmteScripts.PvP.CoreRun
{
    /// Geometry + visuals only. No timers: CoreRunMode drives everything.
    public class CoreRunTore : Area.Tore
    {
        private readonly int _greenModel, _redModel, _greenSpellID, _redSpellID, _innerSpacing, _outerSpacing;
        public int MaxRadius => m_MaxRadius;
        public int ActiveSpellID => DbArea.SpellID;

        public CoreRunTore(string name, Coordinate center, int innerRadius, int outerRadius, int greenModel, int redModel,
                           int greenSpell, int redSpell, int effectAmount, int effectFreq, byte stormLevel, byte stormSize,
                           int effectVariance, Region region, int innerSpacing = 300, int outerSpacing = 50)
        {
            DbArea = new DBArea
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
            LoadFromDatabase(DbArea);
            Region = region;
            _greenModel = greenModel; _redModel = redModel; _greenSpellID = greenSpell; _redSpellID = redSpell;
            _innerSpacing = innerSpacing; _outerSpacing = outerSpacing;
        }

        /// Build both marker rings ONCE per session.
        public void BuildBoundary()
        {
            ClearBoundary();
            SpawnRing(m_Radius, true, _innerSpacing);
            SpawnRing(m_MaxRadius, false, _outerSpacing);
        }

        /// Red/green switch = model swap on the existing markers.
        public void SetLight(bool red)
        {
            DbArea.SpellID = red ? _redSpellID : _greenSpellID;
            SetBoundaryModel((ushort)(red ? _redModel : _greenModel));
        }

        private void SpawnRing(int radius, bool inward, int spacing)
        {
            if (radius <= 0) return;
            int count = Math.Clamp((int)Math.Round(2.0 * Math.PI * radius / spacing), 6, 256);
            for (int i = 0; i < count; i++)
            {
                double t = 2.0 * Math.PI * i / count;
                Angle angle = Angle.Radians(t);
                Vector offset = Vector.Create(angle, radius);
                ushort heading = inward ? Angle.Radians(t + Math.PI).InHeading : angle.InHeading;
                Position pos = Position.Create(Region.ID, Coordinate.X + offset.X, Coordinate.Y + offset.Y, Coordinate.Z, heading);
                if (Region.GetZone(pos.Coordinate) == null) continue;
                GameStaticItem marker = new() { Model = (ushort)_greenModel, Name = "Core Run Boundary", Position = pos };
                marker.AddToWorld();
                m_boundaryObjects.Add(marker);
            }
        }
    }
}