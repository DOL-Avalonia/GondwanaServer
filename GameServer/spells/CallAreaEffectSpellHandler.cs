using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.GS.Scripts;
using DOL.GS.Geometry;

namespace DOL.GS.Spells
{
    [SpellHandler("CallAreaEffect")]
    public class CallAreaEffectSpellHandler : SpellHandler
    {
        public CallAreaEffectSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (selectedTarget == null)
            {
                if (Caster is GamePlayer player)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(player.Client, "TurretsRelease.CheckBeginCast.NoSelectedTarget"), eChatType.CT_SpellResisted);
                }
                return false;
            }

            if (!Caster.IsWithinRadius(selectedTarget, Spell.Range))
            {
                if (Caster is GamePlayer player)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(player.Client, "TurretsRelease.CheckBeginCast.TargetTooFarAway"), eChatType.CT_SpellResisted);
                }
                return false;
            }
            return base.CheckBeginCast(selectedTarget, quiet);
        }

        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            ConsumePower(target);
            base.FinishSpellCast(target, force);
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target is not { ObjectState: GameObject.eObjectState.Active, CurrentRegion: not null })
                return false;
            
            int areaEffectFamily = Spell.LifeDrainReturn;
            if (areaEffectFamily <= 0)
                return false;

            List<DBAreaEffect> areaEffects = GameServer.Database.SelectObjects<DBAreaEffect>(
                DB.Column("AreaEffectFamily").IsEqualTo(areaEffectFamily)).OrderBy(ae => ae.OrderInFamily).ToList();

            if (areaEffects.Count <= 0)
                return false;

            Position initialPosition = target.Position;
            Mob initialMob = GameServer.Database.SelectObjects<Mob>(DB.Column("Mob_ID").IsEqualTo(areaEffects.First().MobID)).FirstOrDefault();
            if (initialMob == null)
                return false;

            SpawnAreaEffect(areaEffects.First(), target, initialPosition, Spell.Duration);

            // Use initialPosition for subsequent AreaEffects
            foreach (var areaEffect in areaEffects.Skip(1))
            {
                Position calculatedPosition = CalculateRelativePosition(initialPosition, areaEffects.First().MobID, areaEffect.MobID);
                SpawnAreaEffect(areaEffect, target, calculatedPosition, Spell.Duration);
            }
            return true;
        }

        private Position CalculateRelativePosition(Position basePosition, string firstMobID, string currentMobID)
        {
            Mob firstMob = GameServer.Database.SelectObjects<Mob>(DB.Column("Mob_ID").IsEqualTo(firstMobID)).FirstOrDefault();
            Mob currentMob = GameServer.Database.SelectObjects<Mob>(DB.Column("Mob_ID").IsEqualTo(currentMobID)).FirstOrDefault();

            if (firstMob == null || currentMob == null)
                return basePosition;

            int deltaX = currentMob.X - firstMob.X;
            int deltaY = currentMob.Y - firstMob.Y;
            int deltaZ = currentMob.Z - firstMob.Z;

            return basePosition.With(
                x: basePosition.X + deltaX,
                y: basePosition.Y + deltaY,
                z: basePosition.Z + deltaZ
            );
        }

        private void SpawnAreaEffect(DBAreaEffect areaEffect, GameLiving target, Position position, int duration)
        {
            var newAreaEffect = AreaEffect.CreateTemporary(Caster, areaEffect, target, position);

            newAreaEffect.LoadedFromScript = true;
            newAreaEffect.AddToWorld();
            newAreaEffect.TempProperties.setProperty("AreaEffectDuration", duration);

            // Set a timer to remove the AreaEffect after the duration
            GameEventMgr.AddHandler(newAreaEffect, GameLivingEvent.RemoveFromWorld, new DOLEventHandler(OnRemoveFromWorld));
            new RemoveAreaEffectTimer(newAreaEffect, duration).Start(duration);
        }

        private void OnRemoveFromWorld(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is AreaEffect areaEffect)
            {
                GameEventMgr.RemoveHandler(areaEffect, GameLivingEvent.RemoveFromWorld, new DOLEventHandler(OnRemoveFromWorld));
                areaEffect.Delete();
            }
        }

        protected class RemoveAreaEffectTimer : RegionAction
        {
            public RemoveAreaEffectTimer(AreaEffect areaEffect, int delay) : base(areaEffect)
            {
            }

            public override void OnTick()
            {
                AreaEffect areaEffect = (AreaEffect)m_actionSource;
                if (areaEffect is { ObjectState: GameObject.eObjectState.Active })
                {
                    areaEffect.RemoveFromWorld();
                }
            }
        }

        public override string GetDelveDescription(GameClient delveClient) => $"{Spell.Name}: {Spell.Description}.";
    }
}