using DOL.Database;
using DOL.Events;
using DOL.GameEvents;
using DOL.gameobjects.CustomNPC;
using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.GS.Spells;
using DOL.Language;
using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static DOL.GS.Spells.BumpSpellHandler;

namespace DOL.GS.Spells
{
    [SpellHandler("BumpSpell")]
    public class BumpSpellHandler : SpellHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        public class BumpTrajectory
        {
            public record Point(Coordinate Coordinate, int Milliseconds);

            public double Gravity => -750.0f;

            public int NumSegments => 9;

            public int MaxSegments => NumSegments * 2;

            public List<Point> Points { get; } = new List<Point>();

            public int HorizontalSpeed { get; set; }

            public double InitialVerticalVelocity { get; set; }

            public Position Start { get; set; }

            public BumpTrajectory(Position start, short height, int distance)
            {
                Start = start;
                InitialVerticalVelocity = Math.Round(Math.Sqrt(2 * height * -Gravity));
                // https://medium.com/@brazmogu/physics-for-game-dev-a-platformer-physics-cheatsheet-f34b09064558
                // z = 0.5 g * t^2 + v * t
                // z = 0 <=> x = (-b +- sqrt(b^2 - 4ac) / 2a)
                //           with a = 0.5g, b = v, c = 0, x = t
                //
                // z = 0 <=> t = (-v +- sqrt(v^2 - 4 * 0.5g * 0)) / 2 * (0.5g)
                //           t = (-v +- sqrt(v^2)) / g
                //           t = (-v +- v) / g
                //           t = 0 and t = -2v / g
                double airTime = 2 * (InitialVerticalVelocity / -Gravity);
                HorizontalSpeed = (short)Math.Round(distance / airTime);
                int msIncrement = Math.Max(1, (int)Math.Round((airTime * 1000) / NumSegments));
                Coordinate currentPoint = Start.Coordinate;
                AddPoint(new Point(currentPoint, 0));
                int timeElapsed = 0;
                for (int i = 0; i < MaxSegments; ++i)
                {
                    RaycastStats stats = new RaycastStats();
                    Coordinate nextPoint = CalculateCoordinateAfter(timeElapsed + msIncrement);
                    if (nextPoint.Equals(currentPoint))
                        continue;

                    float collisionDist = LosCheckMgr.GetCollisionDistance(Start.Region, currentPoint, nextPoint, ref stats);
                    if (log.IsDebugEnabled)
                    {
                        log.DebugFormat("BumpSpellHandler[{0}] {1}ms: {2} to {3}: collision is {4}", i, timeElapsed, currentPoint, nextPoint, collisionDist);
                    }
                    if (!float.IsInfinity(collisionDist))
                    {
                        var vector = (nextPoint - currentPoint);
                        double factor = (collisionDist / vector.Length) - double.Epsilon;
                        if (factor > 0)
                        {
                            timeElapsed = (int)(timeElapsed + msIncrement * factor);
                            nextPoint = currentPoint + (vector * factor);
                            AddPoint(new Point(nextPoint, timeElapsed));
                        }
                        break;
                    }
                    timeElapsed += msIncrement;
                    AddPoint(new Point(nextPoint, timeElapsed));
                    currentPoint = nextPoint;
                }
            }

            private void AddPoint(Point point)
            {
                Points.Add(point);
            }

            public double GetVerticalSpeedAt(int milliseconds)
            {
                double elapsedSeconds = milliseconds / 1000.0;
                return InitialVerticalVelocity + Gravity * elapsedSeconds;
            }

            public Coordinate CalculateCoordinateAfter(int elapsedMilliseconds)
            {
                if (elapsedMilliseconds <= 0)
                {
                    return Start.Coordinate;
                }
                double seconds = (double)elapsedMilliseconds / 1000;
                double z = 0.5 * Gravity * seconds * seconds + InitialVerticalVelocity * seconds;
                Vector v = Vector.Create(Start.Orientation, (int)(HorizontalSpeed * seconds)) + Vector.Create(0, 0, (int)Math.Round(z));
                return Start.Coordinate + v;
            }
        }

        public class Victim
        {
            public GameNPC Npc { get; init; }

            public GameSpellEffect Effect { get; init; }

            public BumpTrajectory Trajectory { get; init; }

            public int CurrentPoint { get; set; }

            public bool IsBumpNpc { get; init; }

            public BumpSpellHandler SpellHandler { get; init; }

            public RegionTimer Timer { get; init; }

            public Victim(BumpSpellHandler spellHandler, GameNPC npc, BumpTrajectory trajectory, GameSpellEffect? effect)
            {
                SpellHandler = spellHandler;
                Npc = npc;
                Trajectory = trajectory;
                CurrentPoint = 0;
                IsBumpNpc = npc is BumpNPC;
                Timer = new RegionTimer(npc, TimerCallback);
                Effect = effect;
            }

            private int TimerCallback(RegionTimer callingtimer)
            {
                if (Npc.ObjectState != GameObject.eObjectState.Active)
                {
                    SpellHandler.Finish(this, 0);
                    return 0;
                }
                int timeNow = Trajectory.Points[CurrentPoint].Milliseconds;
                if (!MoveToNext())
                {
                    SpellHandler.Finish(this, Trajectory.GetVerticalSpeedAt(Trajectory.Points.Last().Milliseconds));
                    return 0;
                }
                int next = Trajectory.Points[CurrentPoint].Milliseconds - timeNow;
                return next;
            }

            public void Start()
            {
                if (Timer is not { IsAlive: false })
                    return;
                
                if (CurrentPoint + 1 >= Trajectory.Points.Count)
                    return;

                int delay = Trajectory.Points[CurrentPoint + 1].Milliseconds - Trajectory.Points[CurrentPoint].Milliseconds;
                if (!MoveToNext())
                {
                    SpellHandler.Finish(this, Trajectory.GetVerticalSpeedAt(Trajectory.Points.Last().Milliseconds));
                    return;
                }

                Timer.Start(delay);
            }

            private bool MoveToNext()
            {
                ++CurrentPoint;
                if (CurrentPoint >= Trajectory.Points.Count)
                {
                    return false;
                }
                Npc.WalkTo(Trajectory.Points[CurrentPoint].Coordinate, (short)Trajectory.HorizontalSpeed);
                //Npc.Motion = Motion.Create(Npc.Position, Trajectory.Points[CurrentPoint].Coordinate, (short)Trajectory.HorizontalSpeed);
                return true;
            }
        }

        private List<Victim> _npcVictims = new();

        class BumpNPC : GameNPC
        {
            public record Entry(GameLiving Living, GameSpellEffect Effect);
            
            public List<Entry> Victims { get; init; } = new List<Entry>();

            public IEnumerable<Entry> ActiveVictims =>
                Victims.Where(e => e.Living.ObjectState is eObjectState.Active
                                  && (e.Living is not GamePlayer player || player.Steed == this));

            public BumpSpellHandler SpellHandler { get; init; }

            public BumpNPC(BumpSpellHandler spellHandler)
            {
                SpellHandler = spellHandler;
            }

            /// <inheritdoc />
            public override bool RiderDismount(bool forced, GamePlayer player)
            {
                if (!base.RiderDismount(forced, player))
                    return false;

                var entry = Victims.Find(e => e.Living == player);
                entry.Effect?.Cancel(false);
                return true;
            }

            /// <inheritdoc />
            public override bool RemoveFromWorld()
            {
                foreach (var (living, effect) in Victims)
                {
                    if (living is GamePlayer player && player.Steed == this)
                        player.DismountSteed(true);
                    else if (!effect.IsExpired)
                        effect.Cancel(false);
                }
                
                return base.RemoveFromWorld();
            }
        }

        public BumpSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
            Height = (int)Spell.Value;
            Distance = (int)Util.Random(Spell.LifeDrainReturn, Spell.AmnesiaChance);
        }

        public int Height
        {
            get;
            set;
        }

        public int Distance
        {
            get;
            set;
        }

        private BumpNPC CreateBumpNPC(GameLiving owner)
        {
            return new BumpNPC(this)
            {
                Realm = Caster.Realm,
                Model = 667,
                Position = owner.Position.With(Caster.Orientation),
                Name = "Bump",
                Level = Caster.Level,
                Flags = GameNPC.eFlags.PEACE | GameNPC.eFlags.DONTSHOWNAME | GameNPC.eFlags.CANTTARGET | GameNPC.eFlags.FLYING,
                MaxSpeedBase = 4000,
                LoadedFromScript = true
            };
        }

        private void DoBumpWithoutServerLos(GameLiving target, double effectiveness)
        {
            // Calculate the bump distance
            int bumpDistance = (int)(Distance * effectiveness);
            int height = (int)(Height * effectiveness);

            if (bumpDistance <= 0 && height <= 0)
                return;

            GamePlayer losPlayer = target as GamePlayer ?? Caster as GamePlayer;
            if (losPlayer is null)
            {
                log.Debug($"Cannot calculate LOS for BumpSpellHandler from ${Caster.Name} to ${target.Name}, neither is a player");
                return;
            }

            GameSpellEffect effect = CreateSpellEffect(target, effectiveness);
            if (!OnDurationEffectApply(target, effect, effectiveness))
                return;

            BumpNPC npc = CreateBumpNPC(target);
            npc.Victims.Add(new(target, effect));
            npc.AddToWorld();
            int maxTries = 4;
            int tries = 1;
            Vector v = Vector.Create(Caster.Position.Orientation, bumpDistance) + Vector.Create(0, 0, height);
            npc.Position = target.Position + v;
            losPlayer.Out.SendCheckLOS(target, npc, Callback); // TODO: Timeout?

            void Callback(GamePlayer gamePlayer, ushort response, ushort sourceoid, ushort targetoid)
            {
                if ((response & 0x100) != 0x100)
                {
                    // LOS failed
                    if (tries >= maxTries)
                    {
                        effect.Cancel(false);
                        npc.RemoveFromWorld();
                        npc.Delete();
                        return;
                    }
                    ++tries;
                    v = Vector.Create(Caster.Position.Orientation, (int)(bumpDistance / tries)) + Vector.Create(0, 0, (int)(height / tries));
                    npc.Position = target.Position + v;
                    losPlayer.Out.SendCheckLOS(target, npc, Callback);
                    return;
                }
                foreach (GamePlayer p in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).Cast<GamePlayer>())
                {
                    p.Out.SendSpellEffectAnimation(m_caster, target, m_spell.ClientEffect, 0, false, 1);
                }

                target.MoveTo(npc.Position);
                effect.Cancel(false);
                npc.RemoveFromWorld();
                npc.Delete();
            }
        }

        public void DoBumpWithServerLos(GameLiving target, double effectiveness)
        {
            // Calculate the bump distance
            int bumpDistance = (int)(Distance * effectiveness);
            int height = (int)(Height * effectiveness);

            if (bumpDistance <= 0 && height <= 0)
                return;

            BumpTrajectory trajectory = null;
            if (target is GamePlayer player)
            {
                BumpNPC npc = (BumpNPC)_npcVictims.FirstOrDefault(n => n.IsBumpNpc && n.Npc.IsWithinRadius2D(target, 8.0f))?.Npc;
                if (npc == null)
                {
                    trajectory = _npcVictims.Find(t => GameMath.IsWithinRadius2D(t.Trajectory.Start.Coordinate, target.Coordinate, 8.0f))?.Trajectory;
                    if (trajectory is not { Points.Count: > 0 })
                    {
                        trajectory = new BumpTrajectory(target.Position.With(Caster.Orientation), (short)height, bumpDistance);
                        if (trajectory.Points.Count <= 1)
                            return;
                    }
                }

                GameSpellEffect effect = CreateSpellEffect(target, effectiveness);
                if (!OnDurationEffectApply(target, effect, effectiveness))
                    return;

                if (npc == null)
                {
                    npc = CreateBumpNPC(target);
                    var victim = new Victim(this, npc, trajectory!, null);
                    npc.AddToWorld();
                    _npcVictims.Add(victim);
                    victim.Start();
                }
                
                npc.Victims.Add(new(player, effect));
                player.MountSteed(npc, true);
            }
            else if (target is GameNPC npc)
            {
                trajectory = _npcVictims.Find(t => GameMath.IsWithinRadius2D(t.Trajectory.Start.Coordinate, target.Coordinate, 8.0f))?.Trajectory;
                if (trajectory is not { Points.Count: > 0 })
                {
                    trajectory = new BumpTrajectory(target.Position.With(Caster.Orientation), (short)height, bumpDistance);
                    if (trajectory.Points.Count <= 1)
                        return;
                }
                
                GameSpellEffect effect = CreateSpellEffect(target, effectiveness);
                if (!OnDurationEffectApply(target, effect, effectiveness))
                    return;

                var victim = new Victim(this, npc, trajectory, effect);
                _npcVictims.Add(victim);
                victim.Start();
            }

            foreach (GamePlayer p in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).Cast<GamePlayer>())
            {
                p.Out.SendSpellEffectAnimation(m_caster, target, m_spell.ClientEffect, 0, false, 1);
            }
        }

        public override bool CheckBeginCast(GameLiving target, bool quiet)
        {
            if (Caster is GamePlayer player && player.IsRiding)
            {
                if (!quiet)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.CastSpell.CannotCastRiding"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }

                return false;
            }

            return base.CheckBeginCast(target, quiet);
        }

        /// <inheritdoc />
        public override void OnEffectRemove(GameSpellEffect effect, bool overwrite)
        {
            base.OnEffectRemove(effect, overwrite);

            if (effect.SpellHandler != this)
                return;

            if (effect.Owner is GamePlayer { Steed: BumpNPC bump } player && bump.SpellHandler == this)
            {
                player.DismountSteed(true);
            }
        }

        private void ApplyStatusEffects(GameLiving living, bool apply)
        {
            if (living is GamePlayer)
            {
                living.IsStunned = apply;
            }
            else
            {
                if (living is GameNPC npc)
                {
                    if (apply)
                    {
                        npc.Brain.Stop();
                        npc.Flags |= GameNPC.eFlags.FLYING;
                    }
                    else
                    {
                        npc.Brain.Start();
                        npc.Flags &= ~(GameNPC.eFlags.FLYING) | ((GameNPC.eFlags)npc.FlagsDb & GameNPC.eFlags.FLYING);
                    }
                }
                living.IsFrozen = apply;
            }
            living.DebuffCategory[(int)eProperty.SpellFumbleChance] += 100 * (apply ? 1 : -1);
        }

        /// <inheritdoc />
        public override void OnEffectStart(GameSpellEffect effect)
        {
            var owner = effect.Owner;
            owner.StopAttack();
            owner.StopCurrentSpellcast();
            owner.Emote(eEmote.Stagger);

            BroadcastMessage(effect.Owner, "is hurled into the air!");

            ApplyStatusEffects(effect.Owner, true);
        }

        /// <inheritdoc />
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            ApplyStatusEffects(effect.Owner, false);
            base.OnEffectExpires(effect, noMessages);
            return Spell.Duration;
        }

        /// <inheritdoc />
        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            return 0; // Infinite duration, handled by the Victim timer
        }

        private void BroadcastMessage(GameLiving target, string message)
        {
            if (Caster is GamePlayer casterPlayer)
            {
                casterPlayer.Out.SendMessage(LanguageMgr.GetTranslation(casterPlayer.Client, "SpellHandler.BumpSpell.TargetHurled", target.GetName(0, false)), eChatType.CT_Broadcast, eChatLoc.CL_SystemWindow);
            }
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (!target.IsAlive || target is ShadowNPC || target is IllusionBladePet || target is AstralPet || target is GameTaxiBoat || target is GameTaxi)
                return false;

            if (IsSwimming(target) || IsPeaceful(target))
                return false;

            if (target is GameNPC npc)
            {
                if (npc is GameTrainingDummy || npc is GameDragon || npc.IsBoss)
                {
                    SendSpellResistAnimation(npc);
                    return false;
                }

                if (npc.Brain is BomberBrain)
                {
                    return false;
                }
            }

            if (target.HasAbility(Abilities.StunImmunity) || target.HasAbility(Abilities.CCImmunity))
            {
                (m_caster as GamePlayer)?.SendTranslatedMessage("SpellHandler.DamageImmunity", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, m_caster.GetPersonalizedName(target));
                return true;
            }

            foreach (var effect in target.FindEffectsOnTarget())
            {
                switch (effect)
                {
                    case AdrenalineSpellEffect:
                        {
                            (m_caster as GamePlayer)?.SendTranslatedMessage("Adrenaline.Target.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, m_caster.GetPersonalizedName(target));
                            (target as GamePlayer)?.SendTranslatedMessage("Adrenaline.Self.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                            return true;
                        }

                    case ChargeEffect:
                        {
                            (m_caster as GamePlayer)?.SendTranslatedMessage("SpellHandler.Target.TooFast", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, m_caster.GetPersonalizedName(target));
                            return true;
                        }

                    case NecromancerShadeEffect:
                        {
                            return false;
                        }
                }
            }

            if (!LosCheckMgr.HasDataFor(target.CurrentZone))
            {
                log.Warn($"BumpSpellHandler: LOSCheckManager has no data for zone { target.CurrentZone.ID }, falling back to simple technique of teleporting the target");
                DoBumpWithoutServerLos(target, effectiveness);
            }
            else
            {
                DoBumpWithServerLos(target, effectiveness);
            }
            return true;
        }

        /// <summary>
        /// Do final effects when the living hits the ground after the full duration of the bump.
        /// </summary>
        /// <param name="living">Entity to apply the effects to</param>
        /// <param name="speed">Vertical speed at this point</param>
        private void DoFinalEffects(GameLiving living, double speed)
        {
            if (living.IsAlive)
            {
                // If hitting the ceiling we also take damage maybe
                var velocity = Math.Abs(speed);
                if (velocity > 0.01)
                {
                    int safeFallLevel = living.GetAbilityLevel(Abilities.SafeFall);
            
                    var fallSpeed = velocity - (100 * safeFallLevel);

                    int fallDivide = 15;
                    int fallMinSpeed = 500;

                    var fallPercent = (int)Math.Min(99, (fallSpeed - (fallMinSpeed + 1)) / fallDivide);

                    if (fallSpeed > fallMinSpeed)
                    {
                        fallPercent = Math.Max(0, fallPercent - safeFallLevel);
                        fallPercent /= 2;
                        living.TakeFallDamage(fallPercent);
                    }
                }
            }
        }

        private void Finish(Victim victim, double speed)
        {
            if (victim.IsBumpNpc)
            {
                var bumpNpc = ((BumpNPC)victim.Npc);
                foreach (GameLiving living in bumpNpc.ActiveVictims.Select(e => e.Living))
                {
                    if (living is GamePlayer player)
                    {
                        if (player.DismountSteed(true))
                            DoFinalEffects(player, speed);
                    }
                    else
                        DoFinalEffects(living, speed);
                }
                victim.Npc.RemoveFromWorld();
                victim.Npc.Delete();
            }
            else
            {
                DoFinalEffects(victim.Npc, speed);
            }
            victim.Effect?.Cancel(false);
            Cleanup(victim);
        }

        private void Cleanup(Victim victim)
        {
            _npcVictims.Remove(victim);
        }

        public override IList<GameLiving> SelectTargets(GameObject castTarget, bool force = false)
        {
            var targets = new List<GameLiving>();
            if (Spell.Target == "enemy")
            {
                if (castTarget is GameLiving livingTarget && livingTarget.IsAlive && livingTarget != Caster)
                {
                    targets.Add(livingTarget);
                }
            }
            else if (Spell.Target == "cone" || Spell.Target == "area")
            {
                targets.AddRange(GetTargetsInArea());
            }
            return targets;
        }

        private IEnumerable<GameLiving> GetTargetsInArea()
        {
            var targets = new List<GameLiving>();
            foreach (GamePlayer player in Caster.GetPlayersInRadius((ushort)Spell.Radius))
            {
                if (player.IsAlive && player != Caster && GameServer.ServerRules.IsAllowedToAttack(Caster, player, true))
                {
                    targets.Add(player);
                }
            }

            foreach (GameNPC npc in Caster.GetNPCsInRadius((ushort)Spell.Radius))
            {
                if (npc.IsAlive && npc != Caster && GameServer.ServerRules.IsAllowedToAttack(Caster, npc, true))
                {
                    targets.Add(npc);
                }
            }
            return targets;
        }

        private bool IsSwimming(GameLiving target)
        {
            if (target is GamePlayer player)
            {
                return player.IsSwimming;
            }
            if (target is GameNPC npc)
            {
                return npc.Flags.HasFlag(GameNPC.eFlags.SWIMMING);
            }
            return false;
        }

        private bool IsPeaceful(GameLiving target)
        {
            if (target is GameNPC npc)
            {
                return npc.Flags.HasFlag(GameNPC.eFlags.PEACE);
            }
            return false;
        }

        public override int CalculateToHitChance(GameLiving target)
        {
            return 100; // Always hit
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            StringBuilder sb = new StringBuilder();

            int height = (int)Spell.Value;
            int minDistancePercent = (int)Spell.LifeDrainReturn;
            int maxDistancePercent = (int)Spell.AmnesiaChance;
            int radius = (int)Spell.Radius;

            string mainDesc;

            if (Spell.Target == "enemy" && radius == 0)
            {
                // Single target version
                mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Bump.MainDescriptionSingle", height, minDistancePercent, maxDistancePercent);
            }
            else if (Spell.Target == "enemy" && radius > 0)
            {
                // Multiple enemies around caster
                mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Bump.MainDescriptionArea", height, minDistancePercent, maxDistancePercent, radius);
            }
            else if (Spell.Target == "cone" && radius > 0)
            {
                // Enemies in a cone before the caster
                mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Bump.MainDescriptionCone", height, minDistancePercent, maxDistancePercent);
            }
            else
            {
                mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Bump.MainDescriptionSingle", height, minDistancePercent, maxDistancePercent);
            }

            sb.Append(mainDesc);

            if (Spell.RecastDelay > 0)
            {
                int recastSeconds = Spell.RecastDelay / 1000;
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                sb.Append("\n\n");
                sb.Append(secondDesc);
            }

            return sb.ToString().TrimEnd();
        }
    }
}