﻿using DOL.AI;
using DOL.Database;
using DOL.GS.Geometry;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS.Spells
{
    [SpellHandler("GroundArea")]
    public class GroundAreaSpellHandler : SpellHandler
    {
        internal class GroundAreaTurret : GameMovingObject
        {
            public GroundAreaTurret(ABrain brain) : base()
            {
                SetOwnBrain(brain);
            }
        }
        
        internal class GroundAreaTurretBrain : ABrain
        {
            public SpellHandler MasterSpellHandler { get; init; }

            private List<Spell> m_spells;

            /// <inheritdoc />
            public override int ThinkInterval => MasterSpellHandler.Spell.Pulse;

            public GroundAreaTurretBrain(SpellHandler handler)
            {
                MasterSpellHandler = handler;
            }

            /// <inheritdoc />
            public override bool Start()
            {
                if (!base.Start())
                    return false;

                if (Body is GamePet pet)
                {
                    foreach (Spell spell in Body.Spells)
                    {
                        pet.ScalePetSpell(spell);
                    }
                }

                return true;
            }

            /// <inheritdoc />
            public override void Think()
            {
                Body.GroundTargetPosition = Body.Position;
                var caster = MasterSpellHandler.Caster;

                foreach (Spell spell in Body.Spells)
                {
                    ISpellHandler spellhandler = ScriptMgr.CreateSpellHandler(Body, spell, SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells));
                    spellhandler.Parent = MasterSpellHandler;
                    if (MasterSpellHandler.Spell.SubSpellDelay > 0)
                    {
                        new SubSpellTimer(caster, spellhandler, caster).Start(MasterSpellHandler.Spell.SubSpellDelay * 1000);
                    }
                    else
                        spellhandler.StartSpell(caster);
                }
            }
        }
        
        public GroundAreaSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public GameNPC Turret
        {
            get;
            private set;
        }

        public RegionTimer DurationTimer
        {
            get;
            private set;
        }

        protected virtual GameNPC CreateTurret()
        {
            var brain = new GroundAreaTurretBrain(this);

            GroundAreaTurret pet = new GroundAreaTurret(brain);
            pet.Model = 2588;
            pet.Name = Spell.Name;
            pet.Realm = Caster.Realm;
            pet.Owner = Caster;
            pet.Flags = GameNPC.eFlags.CANTTARGET;
            pet.Effectiveness = Caster.Effectiveness;
            pet.Spells = GetTurretSpells().ToList();
            return pet;
        }

        protected virtual IEnumerable<Spell> GetTurretSpells()
        {
            List<int> subSpellList = new List<int>();
            if (m_spell.SubSpellID > 0)
                subSpellList.Add(m_spell.SubSpellID);

            return subSpellList.Union(m_spell.MultipleSubSpells).Select(SkillBase.GetSpellByID).Where(s => s != null);
        }

        /// <inheritdoc />
        public override bool StartSpell(GameLiving target, bool force = false)
        {
            m_spellTarget = Caster;

            GameNPC turret = CreateTurret();
            turret.Position = (Caster.GroundTargetPosition == Position.Nowhere ? Caster.Position : Caster.GroundTargetPosition);
            turret.LoadedFromScript = true;
            turret.AddToWorld();
            if (m_spell.ClientEffect > 0)
            {
                foreach (GamePlayer player in turret.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    player.Out.SendSpellEffectAnimation(Caster, turret, m_spell.ClientEffect, 0, false, 1);
                }
            }
            Turret = turret;

            if (Spell.Duration > 0)
            {
                DurationTimer = new RegionTimer(Turret, _ =>
                {
                    Turret.RemoveFromWorld();
                    Turret.Delete();
                    DurationTimer.Stop();
                    DurationTimer = null;
                    return 0;
                }, Spell.Duration /* "Duration does not increase the duration of ground spells." https://darkageofcamelot.com/content/1122b-live-patch-notes */);
            }

            return true;
        }

        /// <inheritdoc />
        public override void CastSubSpells(GameLiving target)
        {
        }
    }
}
