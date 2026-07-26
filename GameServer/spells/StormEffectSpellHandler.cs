using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.ServerProperties;
using DOL.GS.Effects;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.GS.Scripts;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Custom NPC to properly attribute the storms to the original caster.
    /// Safely handles ownership so all kills and aggro are credited directly to the player/mob.
    /// </summary>
    public class StormNPC : GameNPC
    {
        public GameLiving StormOwner { get; set; }

        public override GameLiving GetLivingOwner()
        {
            return StormOwner ?? base.GetLivingOwner();
        }

        public override GameLiving GetController()
        {
            return StormOwner ?? base.GetController();
        }
    }

    [SpellHandler("StormEffect")]
    public class StormEffectSpellHandler : SpellHandler
    {
        public StormEffectSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        // Mappings transposed from the spell database
        public int StormSpellID => Spell.LifeDrainReturn;
        public int EffectAmount => Spell.AmnesiaChance > 0 ? Spell.AmnesiaChance : 1;
        public int StormSize => Spell.ResurrectHealth > 0 ? Spell.ResurrectHealth : 60;
        public double EffectVariance => Spell.ResurrectMana;
        public int DamageType => (int)Spell.DamageType; // 0=Random, 1=Circular, 2=Spiral, 3=Linear
        public int SpiralArms => Spell.Value > 0 ? (int)Spell.Value : 4;

        private long m_startTime;
        private Coordinate m_centerCoord;
        private Angle m_centerHeading;

        // Shared Tracker: Enforces Spell.TargetHardCap over all scattered storm sub-spells
        private HashSet<GameLiving> m_hitTargets = new HashSet<GameLiving>();

        protected override void AutoSelectCaster(ref GameLiving target)
        {
            base.AutoSelectCaster(ref target);
            string tgt = Spell.Target.ToLower();
            
            if (tgt == "enemy" || tgt == "area" || tgt == "cone")
            {
                target = Caster;
            }
        }

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            string tgt = Spell.Target.ToLower();
            
            if (tgt == "ground" && Caster.GroundTargetPosition == Position.Nowhere)
            {
                if (!quiet && Caster is GamePlayer p)
                    p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "SpellHandler.GroundTargetNotInView"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            
            return base.CheckBeginCast(selectedTarget, quiet);
        }

        protected override bool ExecuteSpell(GameLiving target, bool force = false)
        {
            string tgt = Spell.Target.ToLower();

            // Enforce Self-centering unless ground is designated
            if (tgt == "enemy" || tgt == "area" || tgt == "cone")
                target = Caster;

            m_centerCoord = Caster.Coordinate;
            m_centerHeading = Caster.Orientation;

            if (tgt == "ground")
            {
                m_centerCoord = Caster.GroundTargetPosition.Coordinate;
                if (m_centerCoord == Coordinate.Nowhere)
                    m_centerCoord = Caster.Coordinate;
            }

            int radius = Spell.Radius;
            m_startTime = Caster.CurrentRegion.Time;

            lock (m_hitTargets)
            {
                m_hitTargets.Clear();
            }

            if (DamageType == 0)
            {
                // First initial spawn burst
                SpawnRandomStorms(m_centerCoord, radius, tgt == "cone");

                if (Spell.Pulse != 0 && Spell.Frequency > 0)
                {
                    CancelAllPulsingSpells(Caster);
                    PulsingSpellEffect pulseeffect = new PulsingSpellEffect(this);
                    pulseeffect.Start();
                }
            }
            else if (DamageType == 1) // CIRCULAR
            {
                GenerateCircularStorms(m_centerCoord, m_centerHeading, radius, tgt == "cone");
            }
            else if (DamageType == 2) // SPIRAL
            {
                GenerateSpiralStorms(m_centerCoord, m_centerHeading, radius);
            }
            else if (DamageType == 3) // LINEAR
            {
                GenerateLinearStorms(m_centerCoord, m_centerHeading, radius);
            }

            SendLaunchAnimation(Caster, 0, false, 1);
            return true;
        }

        public override void OnSpellPulse(PulsingSpellEffect effect)
        {
            if (Caster == null || !Caster.IsAlive || Caster.ObjectState != GameObject.eObjectState.Active)
            {
                effect.Cancel(false);
                return;
            }

            // Enforce duration limitation on the overall pulse length
            if (Spell.Duration > 0 && (Caster.CurrentRegion.Time - m_startTime) >= Spell.Duration)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Earthquake.Ending"), eChatType.CT_SpellExpires);
                effect.Cancel(false);
                return;
            }

            if (Spell.PulsePower > 0)
            {
                if (Caster.Mana < Spell.PulsePower)
                {
                    if (Spell.IsFocus)
                        FocusSpellAction(null, Caster, null);
                        
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.PulsingSpellNoMana"), eChatType.CT_SpellExpires);
                    effect.Cancel(false);
                    return;
                }
                Caster.Mana -= Spell.PulsePower;
            }

            Coordinate centerCoord = Caster.Coordinate;
            if (Spell.Target.ToLower() == "ground")
            {
                centerCoord = Caster.GroundTargetPosition.Coordinate;
                if (centerCoord == Coordinate.Nowhere)
                    centerCoord = Caster.Coordinate;
            }

            lock (m_hitTargets)
            {
                m_hitTargets.Clear();
            }

            SpawnRandomStorms(centerCoord, Spell.Radius, Spell.Target.ToLower() == "cone");
        }

        private int GetStormLevel()
        {
            int level = Caster.Level;
            if (level <= 10) return level;
            if (level <= 32) return Math.Max(1, level - 2);
            if (level <= 48) return Math.Max(1, level - 4);
            return Math.Max(1, level - 5);
        }

        protected void SpawnRandomStorms(Coordinate centerCoord, int radius, bool isCone)
        {
            int baseAmount = EffectAmount;
            double varFactor = EffectVariance / 100.0;
            int actualAmount = baseAmount;

            if (varFactor > 0)
            {
                int rangeAmt = (int)Math.Round(baseAmount * varFactor);
                actualAmount = Util.Random(Math.Max(1, baseAmount - rangeAmt), baseAmount + rangeAmt);
            }
            actualAmount = Math.Max(1, actualAmount);

            for (int i = 0; i < actualAmount; i++)
            {
                Coordinate pt = GetRandomPoint(centerCoord, radius, isCone ? m_centerHeading : (Angle?)null);
                if (pt != Coordinate.Nowhere)
                    SpawnStormNPC(pt, 1);
            }
        }

        protected Coordinate GetRandomPoint(Coordinate center, int radius, Angle? coneHeading = null)
        {
            if (radius <= 0) return center;
            double r = Math.Sqrt(Util.RandomDouble()) * radius;
            Angle angle;

            if (coneHeading.HasValue)
            {
                // Approx 45 degrees span (-22.5 to +22.5) funnel
                int degOffset = Util.Random(-22, 22);
                angle = coneHeading.Value + Angle.Degrees(degOffset);
            }
            else
            {
                angle = Angle.Degrees(Util.Random(0, 359));
            }

            Vector offset = Vector.Create(angle, r);
            return center + offset;
        }

        protected void GenerateCircularStorms(Coordinate center, Angle heading, int maxRadius, bool isCone)
        {
            if (maxRadius <= 0)
            {
                SpawnStormNPC(center, 1);
                return;
            }

            int delayMs = isCone ? 250 : 350;
            int ringSpacing = 80;
            int npcSpacing = 80;

            int rings = Math.Max(1, maxRadius / ringSpacing);

            for (int i = 0; i <= rings; i++)
            {
                int currentRadius = i * ringSpacing;
                if (currentRadius > maxRadius) currentRadius = maxRadius;

                if (currentRadius == 0)
                {
                    SpawnStormNPC(center, 1);
                    continue;
                }

                double perimeter = isCone ? (Math.PI / 4.0 * currentRadius) : (2.0 * Math.PI * currentRadius);
                int count = Math.Max(1, (int)Math.Round(perimeter / npcSpacing));

                if (isCone)
                {
                    double startAngle = heading.InDegrees - 22.5;
                    double step = count > 1 ? 45.0 / (count - 1) : 0;
                    for (int j = 0; j < count; j++)
                    {
                        Angle ang = Angle.Degrees((int)(startAngle + j * step));
                        SpawnStormNPC(center + Vector.Create(ang, currentRadius), Math.Max(1, delayMs * i));
                    }
                }
                else
                {
                    double step = count > 0 ? 360.0 / count : 0;
                    for (int j = 0; j < count; j++)
                    {
                        Angle ang = Angle.Degrees((int)(j * step));
                        SpawnStormNPC(center + Vector.Create(ang, currentRadius), Math.Max(1, delayMs * i));
                    }
                }
            }
        }

        protected void GenerateSpiralStorms(Coordinate center, Angle heading, int maxRadius)
        {
            if (maxRadius <= 0)
            {
                SpawnStormNPC(center, 1);
                return;
            }

            int arms = SpiralArms;
            int npcSpacing = 60;
            int steps = Math.Max(1, maxRadius / npcSpacing);

            for (int arm = 0; arm < arms; arm++)
            {
                double startAngle = heading.InRadians + (2.0 * Math.PI / arms) * arm;
                int currentDelay = 1;

                for (int step = 1; step <= steps; step++)
                {
                    int currentRadius = step * npcSpacing;
                    if (currentRadius > maxRadius) currentRadius = maxRadius;

                    // Twist the spiral gracefully as we go outward by pi radians
                    double twist = (currentRadius / (double)maxRadius) * Math.PI; 
                    double angle = startAngle + twist;

                    Angle ang = Angle.Radians(angle);
                    Coordinate spawnPt = center + Vector.Create(ang, currentRadius);

                    SpawnStormNPC(spawnPt, currentDelay);
                    currentDelay += Util.Random(150, 200);
                }
            }
        }

        protected void GenerateLinearStorms(Coordinate center, Angle heading, int maxRadius)
        {
            if (maxRadius <= 0)
            {
                SpawnStormNPC(center, 1);
                return;
            }

            int npcSpacing = 50;
            int delayStep = 150;
            int steps = Math.Max(1, maxRadius / npcSpacing);

            for (int step = 1; step <= steps; step++)
            {
                int currentRadius = step * npcSpacing;
                if (currentRadius > maxRadius) currentRadius = maxRadius;

                Coordinate spawnPt = center + Vector.Create(heading, currentRadius);

                SpawnStormNPC(spawnPt, Math.Max(1, delayStep * step));
            }
        }

        protected virtual void SpawnStormNPC(Coordinate point, int delay)
        {
            if (point == Coordinate.Nowhere) return;
            if (Caster.CurrentRegion == null || Caster.CurrentRegion.GetZone(point) == null) return;

            new RegionTimer(Caster, t =>
            {
                if (!Caster.IsAlive || Caster.ObjectState != GameObject.eObjectState.Active) return 0;

                int actualLevel = GetStormLevel();
                int actualSize = StormSize;
                double varFactor = EffectVariance / 100.0;

                // Adjust by Variance bounds
                if (varFactor > 0)
                {
                    int rangeLvl = (int)Math.Round(actualLevel * varFactor);
                    actualLevel = Util.Random(Math.Max(1, actualLevel - rangeLvl), actualLevel + rangeLvl);
                    
                    int rangeSize = (int)Math.Round(StormSize * varFactor);
                    actualSize = Util.Random(Math.Max(1, StormSize - rangeSize), StormSize + rangeSize);
                }

                actualLevel = Math.Max(1, Math.Min(255, actualLevel));
                actualSize = Math.Max(1, Math.Min(255, actualSize));

                StormNPC stormPoint = new StormNPC();
                stormPoint.Model = 667;
                stormPoint.Name = "Storm";
                stormPoint.Flags = GameNPC.eFlags.CANTTARGET | GameNPC.eFlags.DONTSHOWNAME;
                stormPoint.Position = Position.Create(Caster.CurrentRegionID, point, 0);
                stormPoint.Level = (byte)actualLevel;
                stormPoint.Size = (byte)actualSize;
                stormPoint.Realm = Caster.Realm;
                
                // Set attribution accurately
                stormPoint.StormOwner = Caster;

                if (Caster is GameNPC npc && npc.Faction != null)
                {
                    stormPoint.Faction = npc.Faction;
                }

                stormPoint.AddToWorld();

                // Delay effect burst matching exact Area delay mechanics natively
                new RegionTimer(stormPoint, (st) =>
                {
                    if (stormPoint.ObjectState != GameObject.eObjectState.Active) return 0;

                    int stormSpellID = StormSpellID;
                    if (stormSpellID <= 0) return 0;

                    Spell subSpell = SkillBase.GetSpellByID(stormSpellID);
                    if (subSpell != null)
                    {
                        ushort effectID = (ushort)(subSpell.ClientEffect > 0 ? subSpell.ClientEffect : stormSpellID);
                        foreach (GamePlayer player in stormPoint.GetPlayersInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
                        {
                            player.Out.SendSpellEffectAnimation(stormPoint, stormPoint, effectID, 0, false, 1);
                        }

                        SpellLine line = SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells);
                        ISpellHandler handler = ScriptMgr.CreateSpellHandler(stormPoint, subSpell, line);
                        if (handler != null)
                        {
                            handler.Parent = this;

                            if (Spell.TargetHardCap > 0 && handler is SpellHandler sh)
                            {
                                var targets = sh.SelectTargets(stormPoint);
                                var filteredTargets = new List<GameLiving>();

                                lock (m_hitTargets)
                                {
                                    foreach (var tgt in targets)
                                    {
                                        if (m_hitTargets.Contains(tgt))
                                        {
                                            filteredTargets.Add(tgt);
                                        }
                                        else if (m_hitTargets.Count < Spell.TargetHardCap)
                                        {
                                            m_hitTargets.Add(tgt);
                                            filteredTargets.Add(tgt);
                                        }
                                    }
                                }

                                sh.SendLaunchAnimation(stormPoint, 0, false, 1);
                                foreach (GameLiving targetLiving in filteredTargets)
                                {
                                    double effectiveness = stormPoint.Effectiveness;

                                    // Distance based variance
                                    if (sh.Spell.Radius > 0 && sh.Spell.Target.ToLower() != "self" && sh.Spell.Target.ToLower() != "pet")
                                    {
                                        int dist = (int)targetLiving.Coordinate.DistanceTo(stormPoint.Coordinate);
                                        if (dist >= 0) effectiveness -= ((double)dist / sh.Spell.Radius);
                                    }

                                    if (Util.Chance(sh.CalculateSpellResistChance(targetLiving)))
                                    {
                                        sh.OnSpellResisted(targetLiving);
                                    }
                                    else
                                    {
                                        sh.ApplyEffectOnTarget(targetLiving, effectiveness);
                                    }
                                }
                                if (sh.CastSubSpellsWithSpell)
                                {
                                    sh.CastSubSpells(stormPoint);
                                }
                            }
                            else
                            {
                                handler.StartSpell(stormPoint);
                            }
                        }
                    }
                    return 0;
                }).Start(250);

                // Clean-up the NPC reliably
                new RegionTimer(stormPoint, dt => {
                    if (stormPoint.ObjectState == GameObject.eObjectState.Active)
                    {
                        stormPoint.RemoveFromWorld();
                        stormPoint.Delete();
                    }
                    return 0;
                }).Start(3000);

                return 0;
            }).Start(delay);
        }

        public override bool CastSubSpells(GameLiving target)
        {
            return false;
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;

            string patternName = DamageType switch
            {
                1 => Spell.Target.ToLower() == "cone" ? "Cone" : "Circular",
                2 => "Spiral",
                3 => "Linear",
                _ => "Random"
            };

            string arrangement = LanguageMgr.GetTranslation(language, $"StormEffect.Pattern{patternName.Replace("/", "").Replace(" ", "")}");
            if (string.IsNullOrEmpty(arrangement) || arrangement.StartsWith("SpellDescription"))
            {
                arrangement = DamageType == 2 ? $"{SpiralArms}-Arm Spiral" : patternName;
            }

            // Main descriptions
            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.StormEffect.MainDescription1", EffectAmount, arrangement);
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.StormEffect.MainDescription2", StormSpellID, StormSize, EffectVariance);
            string description = mainDesc1 + "\n" + mainDesc2;

            if (EffectVariance > 0)
            {
                string subDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.StormEffect.SubDescription1", EffectVariance);
                description += "\n\n" + subDesc1;
            }

            if (DamageType == 2)
            {
                string subDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.StormEffect.SubDescription2", SpiralArms);
                description += "\n\n" + subDesc2;
            }

            if (Spell.TargetHardCap > 0)
            {
                string subDesc3 = LanguageMgr.GetTranslation(language, "SpellDescription.StormEffect.SubDescription3", Spell.TargetHardCap);
                description += "\n\n" + subDesc3;
            }

            if (Spell.Pulse > 0 && Spell.Frequency > 0)
            {
                string pulseDesc = LanguageMgr.GetTranslation(language, "SpellDescription.StormEffect.PulseDescription", (Spell.Frequency / 1000.0));
                description += "\n\n" + pulseDesc;
            }

            if (Spell.RecastDelay > 0)
            {
                int recastSeconds = Spell.RecastDelay / 1000;
                string subDesc4 = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                description += "\n\n" + subDesc4;
            }

            if (StormSpellID > 0)
            {
                Spell subSpell = SkillBase.GetSpellByID(StormSpellID);
                if (subSpell != null)
                {
                    string subTitle = LanguageMgr.GetTranslation(language, "SpellDescription.StormEffect.SubSpellTitle", subSpell.Name);
                    description += "\n\n" + subTitle + "\n";

                    ISpellHandler subHandler = ScriptMgr.CreateSpellHandler(Caster, subSpell, SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells));
                    if (subHandler != null)
                    {
                        description += subHandler.GetDelveDescription(delveClient);
                    }
                }
            }

            return description.TrimEnd();
        }
    }
}