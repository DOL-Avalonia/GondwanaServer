using AmteScripts.Managers;
using DOL.Database;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

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

        protected override int EffectLoopCallback(RegionTimer timer)
        {
            if (DbArea == null || Region == null) return 0;

            var pvpMgr = PvpManager.Instance;
            if (pvpMgr == null || !pvpMgr.IsOpen || pvpMgr.CurrentSessionType != PvpManager.eSessionTypes.CoreRun)
            {
                return Math.Max(1000, DbArea.EffectFrequency > 0 ? DbArea.EffectFrequency : 5000);
            }

            int activeSpellID = DbArea.SpellID; // Gets toggled between Green and Red spell IDs in UpdateVisuals
            if (activeSpellID <= 0) return Math.Max(1000, DbArea.EffectFrequency > 0 ? DbArea.EffectFrequency : 5000);

            int baseAmount = DbArea.EffectAmount;
            double varFactor = DbArea.EffectVariance / 100.0;
            int actualLevel = DbArea.StormLevel > 0 ? DbArea.StormLevel : 1;

            if (varFactor > 0)
            {
                int rangeLvl = (int)Math.Round(actualLevel * varFactor);
                actualLevel = Util.Random(actualLevel - rangeLvl, actualLevel + rangeLvl);
            }
            actualLevel = Math.Max(1, Math.Min(255, actualLevel));

            var playersInArea = this.Players;

            List<GamePlayer> activeCenters = new List<GamePlayer>();

            foreach (var player in playersInArea)
            {
                if (player == null || !player.IsInPvP || !player.IsAlive)
                    continue;

                bool skipPlayer = false;
                foreach (var center in activeCenters)
                {
                    if (player.Coordinate.DistanceTo(center.Coordinate) < 390)
                    {
                        skipPlayer = true;
                        break;
                    }
                }

                if (skipPlayer)
                    continue;

                int actualAmount = baseAmount;
                if (varFactor > 0)
                {
                    int rangeAmt = (int)Math.Round(baseAmount * varFactor);
                    actualAmount = Util.Random(baseAmount - rangeAmt, baseAmount + rangeAmt);
                }
                actualAmount = Math.Max(1, actualAmount);

                for (int i = 0; i < actualAmount; i++)
                {
                    Coordinate randomPoint = GetRandomPointAroundPlayer(player, 900);

                    int attempts = 5;
                    bool inTore = false;
                    for (int j = 0; j < attempts; j++)
                    {
                        if (this.IsContaining(randomPoint))
                        {
                            inTore = true;
                            break;
                        }
                        randomPoint = GetRandomPointAroundPlayer(player, 900);
                    }

                    if (!inTore)
                        continue;

                    bool inOverlap = false;
                    foreach (var center in activeCenters)
                    {
                        if (randomPoint.DistanceTo(center.Coordinate) <= 900)
                        {
                            inOverlap = true;
                            break;
                        }
                    }

                    if (inOverlap)
                        continue;

                    if (Region.GetZone(randomPoint) == null)
                        continue;

                    SpawnStorm(randomPoint, actualLevel, activeSpellID);
                }

                activeCenters.Add(player);
            }

            int baseFreq = DbArea.EffectFrequency;
            int nextInterval = baseFreq;
            if (varFactor > 0)
            {
                int varMs = (int)(baseFreq * varFactor);
                nextInterval = Util.Random(baseFreq - varMs, baseFreq + varMs);
            }
            return Math.Max(500, nextInterval);
        }

        private Coordinate GetRandomPointAroundPlayer(GamePlayer player, int maxRadius)
        {
            double angle = Util.RandomDouble() * Math.PI * 2;
            double r = Math.Sqrt(Util.RandomDouble()) * maxRadius;
            int newX = player.Coordinate.X + (int)(r * Math.Cos(angle));
            int newY = player.Coordinate.Y + (int)(r * Math.Sin(angle));

            return Coordinate.Create(newX, newY, player.Coordinate.Z);
        }

        private void SpawnStorm(Coordinate spot, int level, int activeSpellID)
        {
            GameNPC stormPoint = new GameNPC();
            stormPoint.Model = 667;
            stormPoint.Name = "Storm";
            stormPoint.Flags = GameNPC.eFlags.CANTTARGET | GameNPC.eFlags.DONTSHOWNAME;
            stormPoint.Position = Position.Create(Region.ID, spot.X, spot.Y, spot.Z, 0);
            stormPoint.Level = (byte)level;
            stormPoint.Size = DbArea.StormSize > 0 ? DbArea.StormSize : (byte)50;

            if (DbArea.StormFaction > 0)
                stormPoint.Faction = FactionMgr.GetFactionByID(DbArea.StormFaction);

            if (DbArea.NPCImmunToStorm)
                stormPoint.TempProperties.setProperty("NPCImmunToStorm", true);

            stormPoint.AddToWorld();

            new RegionTimer(stormPoint, (t) =>
            {
                if (stormPoint.ObjectState != GameObject.eObjectState.Active) return 0;

                Spell spell = SkillBase.GetSpellByID(activeSpellID);
                ushort effectID = (ushort)(spell != null ? spell.ClientEffect : activeSpellID);

                foreach (GamePlayer p in Region.GetPlayersInRadius(spot, (ushort)WorldMgr.VISIBILITY_DISTANCE, false, false))
                {
                    p.Out.SendSpellEffectAnimation(stormPoint, stormPoint, effectID, 0, false, 1);
                }

                if (spell != null)
                {
                    SpellLine line = SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells);
                    ISpellHandler handler = ScriptMgr.CreateSpellHandler(stormPoint, spell, line);
                    if (handler != null)
                        handler.StartSpell(stormPoint);
                }
                return 0;
            }).Start(250);

            new RegionTimer(stormPoint, t => {
                if (stormPoint.ObjectState == GameObject.eObjectState.Active) stormPoint.Delete();
                return 0;
            }).Start(3000);
        }
    }
}