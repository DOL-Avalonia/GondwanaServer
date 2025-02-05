/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */

using System;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.Database;
using DOL.Events;
using DOL.GS.Geometry;
using System.Collections;
using System.Collections.Generic;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Spells
{
    //http://www.camelotherald.com/masterlevels/ma.php?ml=Stormlord
    //shared timer 1
    #region Stormlord-1
    [SpellHandlerAttribute("DazzlingArray")]
    public class DazzlingArraySpellHandler : StormSpellHandler
    {
        // constructor
        public DazzlingArraySpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            //Construct a new storm.
            storm = new GameStorm();
            storm.Realm = caster.Realm;
            storm.Position = caster.Position;
            storm.Owner = (GamePlayer)caster;
            storm.Movable = true;

            // Construct the storm spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7210;
            dbs.ClientEffect = 7210;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "realm";
            dbs.Radius = 0;
            dbs.Type = "StormMissHit";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth; // should be 4
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            sRadius = 350;
            s = new Spell(dbs, 1);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            tempest = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.DazzlingArray.MainDescription1", Spell.Value);
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.DazzlingArray.MainDescription2");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    [SpellHandlerAttribute("StormMissHit")]
    public class StormMissHit : MasterlevelBuffHandling
    {
        public override eProperty Property1 { get { return eProperty.MissHit; } }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GameSpellEffect neweffect = CreateSpellEffect(target, effectiveness);
            if (target is not { IsAlive: true, ObjectState: GameObject.eObjectState.Active }) return false;
            neweffect.Start(target);

            if (target is GamePlayer)
                ((GamePlayer)target).Out.SendMessage("You're harder to hit!", eChatType.CT_YouWereHit, eChatLoc.CL_SystemWindow);
            return true;
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {

            effect.Owner.EffectList.Remove(effect);
            return base.OnEffectExpires(effect, noMessages);
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        // constructor
        public StormMissHit(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.StormMissHit.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                int recastSeconds = Spell.RecastDelay / 1000;
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //no shared timer
    #region Stormlord-2
    [SpellHandlerAttribute("VacuumVortex")]
    public class VacuumVortexSpellHandler : SpellHandler
    {
        /// <summary>
        /// Execute direct damage spell
        /// </summary>
        /// <param name="target"></param>
        public override void FinishSpellCast(GameLiving target)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }

        public override IList<GameLiving> SelectTargets(GameObject CasterTarget, bool force = false)
        {
            var list = new List<GameLiving>(8);
            foreach (GameNPC storms in Caster.GetNPCsInRadius(350))
            {
                if ((storms is GameStorm) && (GameServer.ServerRules.IsSameRealm(storms, Caster, true))) list.Add(storms);
            }
            return list;
        }
        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }
        /// <summary>
        /// execute non duration spell effect on target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="effectiveness"></param>
        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            //base.OnDirectEffect(target, effectiveness);
            var targets = SelectTargets(Caster); // TODO: Why is this here? This should be in ExecuteSpell

            if (targets == null || targets.Count == 0) return false;

            foreach (GameStorm targetStorm in targets)
            {
                if (targetStorm.Movable)
                {
                    GameNPC targetNPC = targetStorm as GameNPC;
                    int range = Util.Random(0, 750);
                    double angle = Util.RandomDouble() * 2 * Math.PI;
                    var offset = Vector.Create(x: (int)(range * Math.Cos(angle)), y: (int)(range * Math.Sin(angle)) );
                    targetNPC.WalkTo(targetNPC.Coordinate + offset, targetNPC.MaxSpeed);
                }
            }
            return true;
        }

        public VacuumVortexSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.VacuumVortex.MainDescription");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //shared timer 2
    #region Stormlord-3
    [SpellHandlerAttribute("EnervatingGas")]
    public class EnervatingGasSpellHandler : StormSpellHandler
    {
        // constructor
        public EnervatingGasSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            //Construct a new storm.
            storm = new GameStorm();
            storm.Realm = caster.Realm;
            storm.Position = caster.Position;
            storm.Owner = (GamePlayer)caster;
            storm.Movable = true;



            // Construct the storm spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7273;
            dbs.ClientEffect = 7273;
            dbs.Damage = Math.Abs(spell.Damage);
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "enemy";
            dbs.Radius = 0;
            dbs.Type = "StormEnduDrain";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth; //should be 2
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            sRadius = 350;
            s = new Spell(dbs, 1);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            tempest = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.EnervatingGas.MainDescription", Spell.Damage);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    [SpellHandlerAttribute("StormEnduDrain")]
    public class StormEndudrain : SpellHandler
    {

        public StormEndudrain(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GameSpellEffect neweffect = CreateSpellEffect(target, effectiveness);

            neweffect.Start(target);

            if (target is not { IsAlive: true, ObjectState: GameObject.eObjectState.Active }) return false;
            //spell damage should 25;
            int end = (int)(Spell.Damage);
            target.ChangeEndurance(target, GameLiving.eEnduranceChangeType.Spell, (-end));

            if (target is GamePlayer)
                ((GamePlayer)target).Out.SendMessage(" You lose " + end + " endurance!", eChatType.CT_YouWereHit, eChatLoc.CL_SystemWindow);
            (m_caster as GamePlayer)!.Out.SendMessage("" + (m_caster as GamePlayer)!.GetPersonalizedName(target) + " loses " + end + " endurance!", eChatType.CT_YouWereHit, eChatLoc.CL_SystemWindow);
            return true;
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {

            effect.Owner.EffectList.Remove(effect);
            return base.OnEffectExpires(effect, noMessages);
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.StormEndudrain.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //shared timer 1
    #region Stormlord-4
    [SpellHandlerAttribute("InebriatingFumes")]
    public class InebriatingFumesSpellHandler : StormSpellHandler
    {
        // constructor
        public InebriatingFumesSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            //Construct a new storm.
            storm = new GameStorm();
            storm.Realm = caster.Realm;
            storm.Position = caster.Position;
            storm.Owner = (GamePlayer)caster;
            storm.Movable = true;

            // Construct the storm spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7258;
            dbs.ClientEffect = 7258;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.Damage;
            dbs.Target = "enemy";
            dbs.Radius = 0;
            dbs.Type = "StormDexQuickDebuff";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth; // should be 2
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            sRadius = 350;
            s = new Spell(dbs, 1);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            tempest = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.InebriatingFumes.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    /// <summary>
    /// Dex/Qui stat specline debuff
    /// </summary>
    [SpellHandlerAttribute("StormDexQuickDebuff")]
    public class StormDexQuickDebuff : DualStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.Dexterity; } }
        public override eProperty Property2 { get { return eProperty.Quickness; } }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GameSpellEffect neweffect = CreateSpellEffect(target, effectiveness);
            if (target is not { IsAlive: true, ObjectState: GameObject.eObjectState.Active }) return false;
            neweffect.Start(target);

            if (target is GamePlayer)
                ((GamePlayer)target).Out.SendMessage("Your dexterity and quickness decreased!", eChatType.CT_YouWereHit, eChatLoc.CL_SystemWindow);
            return true;
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {

            effect.Owner.EffectList.Remove(effect);
            return base.OnEffectExpires(effect, noMessages);
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        // constructor
        public StormDexQuickDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.StormDexQuickDebuff.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                int recastSeconds = Spell.RecastDelay / 1000;
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //shared timer 2
    #region Stormlord-5
    [SpellHandlerAttribute("MentalSiphon")]
    public class MentalSiphonSpellHandler : StormSpellHandler
    {
        // constructor
        public MentalSiphonSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            //Construct a new storm.
            storm = new GameStorm();
            storm.Realm = caster.Realm;
            storm.Position = caster.Position;
            storm.Owner = (GamePlayer)caster;
            storm.Movable = true;

            // Construct the storm spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7303;
            dbs.ClientEffect = 7303;
            dbs.Damage = Math.Abs(spell.Damage);
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "enemy";
            dbs.Radius = 0;
            dbs.Type = "PowerDrainStorm";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth; // should be 2
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            sRadius = 350;
            s = new Spell(dbs, 1);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            tempest = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.MentalSiphon.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }

    [SpellHandlerAttribute("PowerDrainStorm")]
    public class PowerDrainStormSpellHandler : SpellHandler
    {
        public PowerDrainStormSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GameSpellEffect neweffect = CreateSpellEffect(target, effectiveness);
            if (target is not { IsAlive: true, ObjectState: GameObject.eObjectState.Active }) return false;
            neweffect.Start(target);
            int mana = (int)(Spell.Damage);
            target.ChangeMana(target, GameLiving.eManaChangeType.Spell, (-mana));

            if (target is GamePlayer)
            {
                ((GamePlayer)target).Out.SendMessage(target.GetPersonalizedName(m_caster) + " steals you " + mana + " points of power!", eChatType.CT_YouWereHit, eChatLoc.CL_SystemWindow);
            }

            StealMana(target, mana);
            // target.StartInterruptTimer(SPELL_INTERRUPT_DURATION, AttackData.eAttackType.Spell, Caster);
            return true;
        }


        public virtual void StealMana(GameLiving target, int mana)
        {
            if (!m_caster.IsAlive) return;
            m_caster.ChangeMana(target, GameLiving.eManaChangeType.Spell, mana);
            SendCasterMessage(target, mana);

        }


        public virtual void SendCasterMessage(GameLiving target, int mana)
        {
            MessageToCaster(string.Format("You steal {0} for {1} power!", m_caster.GetPersonalizedName(target), mana), eChatType.CT_YouHit);
            if (mana > 0)
            {
                MessageToCaster("You steal " + mana + " power points" + (mana == 1 ? "." : "s."), eChatType.CT_Spell);
            }
            //else
            //{
            //   MessageToCaster("You cannot absorb any more power.", eChatType.CT_SpellResisted);
            //}
        }
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            effect.Owner.EffectList.Remove(effect);
            return base.OnEffectExpires(effect, noMessages);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.PowerDrainStorm.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                int recastSeconds = Spell.RecastDelay / 1000;
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }

    #endregion

    //no shared timer
    #region Stormlord-6
    [SpellHandlerAttribute("FocusingWinds")]
    public class FocusingWindsSpellHandler : SpellHandler
    {
        private GameSpellEffect m_effect;
        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            m_effect = effect;
            if (effect.Owner is GameStorm)
            {
                GameStorm targetStorm = effect.Owner as GameStorm;
                targetStorm!.Movable = false;
                MessageToCaster("Now the vortex of this storm is locked!", eChatType.CT_YouWereHit);
                GameEventMgr.AddHandler(m_caster, GameLivingEvent.Moving, new DOLEventHandler(LivingMoves));
            }
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            if (effect.Owner is GameStorm)
            {
                GameStorm targetStorm = effect.Owner as GameStorm;
                targetStorm!.Movable = true;
                GameEventMgr.RemoveHandler(m_caster, GameLivingEvent.Moving, new DOLEventHandler(LivingMoves));
            }
            return base.OnEffectExpires(effect, noMessages);
        }

        public void LivingMoves(DOLEvent e, object sender, EventArgs args)
        {
            GameLiving player = sender as GameLiving;
            if (player == null) return;
            if (e == GameLivingEvent.Moving)
            {
                MessageToCaster("You are moving. Your concentration fades", eChatType.CT_SpellExpires);
                OnEffectExpires(m_effect, true);
                return;
            }
        }
        public FocusingWindsSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.FocusingWinds.MainDescription");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //shared timer 1
    #region Stormlord-7
    [SpellHandlerAttribute("ChokingVapors")]
    public class ChokingVaporsSpellHandler : StormSpellHandler
    {
        // constructor
        public ChokingVaporsSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            //Construct a new storm.
            storm = new GameStorm();
            storm.Realm = caster.Realm;
            storm.Position = caster.Position;
            storm.Owner = (GamePlayer)caster;
            storm.Movable = true;

            // Construct the storm spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7223;
            dbs.ClientEffect = 7223;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "enemy";
            dbs.Radius = 0;
            dbs.Type = "StormStrConstDebuff";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth; // should be 2
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            sRadius = 350;
            s = new Spell(dbs, 1);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            tempest = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.ChokingVapors.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    /// <summary>
    /// Str/Con stat specline debuff
    /// </summary>
    [SpellHandlerAttribute("StormStrConstDebuff")]
    public class StormStrConstDebuff : DualStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.Strength; } }
        public override eProperty Property2 { get { return eProperty.Constitution; } }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GameSpellEffect neweffect = CreateSpellEffect(target, effectiveness);
            if (target is not { IsAlive: true, ObjectState: GameObject.eObjectState.Active }) return false;
            neweffect.Start(target);

            if (target is GamePlayer)
                ((GamePlayer)target).Out.SendMessage("Your strenght and constitution decreased!", eChatType.CT_YouWereHit, eChatLoc.CL_SystemWindow);
            return true;
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {

            effect.Owner.EffectList.Remove(effect);
            return base.OnEffectExpires(effect, noMessages);
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        // constructor
        public StormStrConstDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.StormStrConstDebuff.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                int recastSeconds = Spell.RecastDelay / 1000;
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //shared timer 1
    #region Stormlord-8
    [SpellHandlerAttribute("SenseDullingCloud")]
    public class SenseDullingCloudSpellHandler : StormSpellHandler
    {
        // constructor
        public SenseDullingCloudSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            //Construct a new storm.
            storm = new GameStorm();
            storm.Realm = caster.Realm;
            storm.Position = caster.Position;
            storm.Owner = (GamePlayer)caster;
            storm.Movable = true;

            // Construct the storm spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7305;
            dbs.ClientEffect = 7305;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "enemy";
            dbs.Radius = 0;
            dbs.Type = "StormAcuityDebuff";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth; // should be 2
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            sRadius = 350;
            s = new Spell(dbs, 1);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            tempest = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.SenseDullingCloud.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    /// <summary>
    /// Acuity stat baseline debuff
    /// </summary>
    [SpellHandlerAttribute("StormAcuityDebuff")]
    public class StormAcuityDebuff : SingleStatDebuff
    {
        public override eProperty Property1 => eProperty.Acuity;

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GameSpellEffect neweffect = CreateSpellEffect(target, effectiveness);
            if (target is not { IsAlive: true, ObjectState: GameObject.eObjectState.Active }) return false;
            neweffect.Start(target);

            if (target is GamePlayer)
                ((GamePlayer)target).Out.SendMessage("Your acuity decreased!", eChatType.CT_YouWereHit, eChatLoc.CL_SystemWindow);
            return true;
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {

            effect.Owner.EffectList.Remove(effect);
            return base.OnEffectExpires(effect, noMessages);
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        // constructor
        public StormAcuityDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.StormAcuityDebuff.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                int recastSeconds = Spell.RecastDelay / 1000;
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //no shared timer
    #region Stormlord-9
    [SpellHandlerAttribute("EnergyTempest")]
    public class EnergyTempestSpellHandler : StormSpellHandler
    {
        // constructor
        public EnergyTempestSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            //Construct a new storm.
            storm = new GameStorm();
            storm.Realm = caster.Realm;
            storm.Position = caster.Position;
            storm.Owner = (GamePlayer)caster;
            storm.Movable = true;

            // Construct the storm spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7216;
            dbs.ClientEffect = 7216;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "enemy";
            dbs.Radius = 0;
            dbs.Type = "StormEnergyTempest";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth;
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            sRadius = 350;
            s = new Spell(dbs, 1);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            tempest = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.EnergyTempest.MainDescription", Spell.Damage);
            string RvRDesc = LanguageMgr.GetTranslation(language, "SpellDescription.FocusShell.OnlyInPvPORvR");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc + "\n\n" + RvRDesc;
            }

            return mainDesc + "\n\n" + RvRDesc;
        }
    }
    [SpellHandlerAttribute("StormEnergyTempest")]
    public class StormEnergyTempest : SpellHandler
    {
        public override double CalculateDamageBase(GameLiving target)
        {
            GamePlayer player = Caster as GamePlayer;

            // % damage procs
            if (Spell.Damage < 0)
            {
                double spellDamage = 0;

                if (player != null)
                {
                    // This equation is used to simulate live values - Tolakram
                    spellDamage = (target.MaxHealth * -Spell.Damage * .01) / 2.5;
                }

                if (spellDamage < 0)
                    spellDamage = 0;

                return spellDamage;
            }

            return base.CalculateDamageBase(target);
        }

        public override double DamageCap(double effectiveness)
        {
            if (Spell.Damage < 0)
            {
                return (m_spellTarget.MaxHealth * -Spell.Damage * .01) * 3.0 * effectiveness;
            }

            return base.DamageCap(effectiveness);
        }
        
        public override bool OnDurationEffectApply(GameLiving target, double effectiveness)
        {
            GameSpellEffect neweffect = CreateSpellEffect(target, effectiveness);
            if (target == null) return false;
            if (!target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active) return false;
            neweffect.Start(target);


            // calc damage
            AttackData ad = CalculateDamageToTarget(target, effectiveness);

            // Attacked living may modify the attack data.
            ad.Target.ModifyAttack(ad);
            SendDamageMessages(ad);

            DamageTarget(ad, true);
            target.StartInterruptTimer(target.SpellInterruptDuration, ad.AttackType, Caster);
            return true;
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {

            effect.Owner.EffectList.Remove(effect);
            return base.OnEffectExpires(effect, noMessages);
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        public StormEnergyTempest(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;
            string spellTarget = LanguageMgr.GetTargetOfType(language, m_spell.Target.ToString());

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Stormlord.StormEnergyTempest.MainDescription", spellTarget, Spell.Value, Spell.Duration);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //ML 10 Arcing Power - already handled in another area
}
