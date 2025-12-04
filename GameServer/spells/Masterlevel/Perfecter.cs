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
 */

using System;
using DOL.GS;
using System.Collections.Generic;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.Database;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Spells
{
    //http://www.camelotherald.com/masterlevels/ma.php?ml=Perfector
    //the link isnt corrently working so correct me if you see any timers wrong.


    //ML1 Cure NS - already handled in another area

    //ML2 GRP Cure Disease - already handled in another area

    //shared timer 1
    #region Perfecter-3
    [SpellHandlerAttribute("FOH")]
    public class FOHSpellHandler : FontSpellHandler
    {
        // constructor
        public FOHSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            ApplyOnNPC = true;

            //Construct a new font.
            font = new GameFont();
            font.Model = 2585;
            font.Name = spell.Name;
            font.Realm = caster.Realm;
            font.Position = caster.Position;
            font.Owner = (GamePlayer)caster;

            // Construct the font spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7245;
            dbs.ClientEffect = 7245;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "realm";
            dbs.Radius = 0;
            dbs.Type = "HealOverTime";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth;
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            dbs.Message1 = spell.Message1;
            dbs.Message2 = spell.Message2;
            dbs.Message3 = spell.Message3;
            dbs.Message4 = spell.Message4;
            sRadius = 350;
            s = new Spell(dbs, 1);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            heal = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Perfector.FOH.MainDescription1", Spell.Value);
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Perfector.FOH.MainDescription2");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    #endregion

    //ML4 Greatness - passive increases 20% concentration

    //shared timer 1
    #region Perfecter-5
    [SpellHandlerAttribute("FOP")]
    public class FOPSpellHandler : FontSpellHandler
    {
        // constructor
        public FOPSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            ApplyOnNPC = false;

            //Construct a new font.
            font = new GameFont();
            font.Model = 2583;
            font.Name = spell.Name;
            font.Realm = caster.Realm;
            font.Position = caster.Position;
            font.Owner = (GamePlayer)caster;

            // Construct the font spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7212;
            dbs.ClientEffect = 7212;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "realm";
            dbs.Radius = 0;
            dbs.Type = "PowerOverTime";
            dbs.Value = spell.Value;
            dbs.Duration = spell.ResurrectHealth;
            dbs.Frequency = spell.ResurrectMana;
            dbs.Pulse = 0;
            dbs.PulsePower = 0;
            dbs.LifeDrainReturn = spell.LifeDrainReturn;
            dbs.Power = 0;
            dbs.CastTime = 0;
            dbs.Range = WorldMgr.VISIBILITY_DISTANCE;
            dbs.Message1 = spell.Message1;
            dbs.Message2 = spell.Message2;
            dbs.Message3 = spell.Message3;
            dbs.Message4 = spell.Message4;
            sRadius = 350;
            s = new Spell(dbs, 1);
            sl = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            heal = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Perfector.FOP.MainDescription1", Spell.Value);
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Perfector.FOP.MainDescription2");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    #endregion

    //shared timer 1
    #region Perfecter-6
    [SpellHandlerAttribute("FOR")]
    public class FORSpellHandler : FontSpellHandler
    {
        // constructor
        public FORSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            ApplyOnNPC = true;
            ApplyOnCombat = true;

            //Construct a new font.
            font = new GameFont();
            font.Model = 2581;
            font.Name = spell.Name;
            font.Realm = caster.Realm;
            font.Position = caster.Position;
            font.Owner = (GamePlayer)caster;

            // Construct the font spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7214;
            dbs.ClientEffect = 7214;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "realm";
            dbs.Radius = 0;
            dbs.Type = "MesmerizeDurationBuff";
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
            heal = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Perfector.FOR.MainDescription", Spell.Value);

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
    //ML7 Leaping Health - already handled in another area

    //no shared timer
    #region Perfecter-8
    [SpellHandlerAttribute("SickHeal")]
    public class SickHealSpellHandler : RemoveSpellEffectHandler
    {
        // constructor
        public SickHealSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            var types = new List<string>();
            types.Add("PveResurrectionIllness");
            types.Add("RvrResurrectionIllness");
            SpellTypesToRemove = types;
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Perfector.SickHeal.MainDescription");

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
    #region Perfecter-9
    [SpellHandlerAttribute("FOD")]
    public class FODSpellHandler : FontSpellHandler
    {
        // constructor
        public FODSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            ApplyOnCombat = true;

            //Construct a new font.
            font = new GameFont();
            font.Model = 2582;
            font.Name = spell.Name;
            font.Realm = caster.Realm;
            font.Position = caster.Position;
            font.Owner = (GamePlayer)caster;

            // Construct the font spell
            dbs = new DBSpell();
            dbs.Name = spell.Name;
            dbs.Icon = 7310;
            dbs.ClientEffect = 7310;
            dbs.Damage = spell.Damage;
            dbs.DamageType = (int)spell.DamageType;
            dbs.Target = "enemy";
            dbs.Radius = 0;
            dbs.Type = "PowerRend";
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
            heal = ScriptMgr.CreateSpellHandler(m_caster, s, sl);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Perfector.FOD.MainDescription", Spell.Value);

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
    //ML10 Rampant Healing - already handled in another area

    #region PoT
    [SpellHandlerAttribute("PowerOverTime")]
    public class PoTSpellHandler : SpellHandler
    {
        /// <summary>
        /// Execute heal over time spell
        /// </summary>
        /// <param name="target"></param>
        /// <param name="force"></param>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            // TODO: correct formula
            double eff = 1.25;
            if (Caster is GamePlayer)
            {
                double lineSpec = Caster.GetModifiedSpecLevel(m_spellLine.Spec);
                if (lineSpec < 1)
                    lineSpec = 1;
                eff = 0.75;
                if (Spell.Level > 0)
                {
                    eff += (lineSpec - 1.0) / Spell.Level * 0.5;
                    if (eff > 1.25)
                        eff = 1.25;
                }
            }
            return base.ApplyEffectOnTarget(target, eff);
        }

        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            return new GameSpellEffect(this, Spell.Duration, Spell.Frequency, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            SendEffectAnimation(effect.Owner, 0, false, 1);
            //"{0} seems calm and healthy."
            foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (!(effect.Owner == player || m_caster == player))
                {
                    player.MessageFromArea(effect.Owner, Util.MakeSentence(Spell.Message2,
                        player.GetPersonalizedName(effect.Owner)), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }
        }

        public override void OnEffectPulse(GameSpellEffect effect)
        {
            base.OnEffectPulse(effect);
            OnDirectEffect(effect.Owner, effect.Effectiveness);
        }

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target is not { InCombat: false, ObjectState: GameObject.eObjectState.Active, IsAlive: false })
            {
                return false;
            }
            
            if (target is GamePlayer { CharacterClass.ID: (int)eCharacterClass.Vampiir or (int)eCharacterClass.MaulerHib or (int)eCharacterClass.MaulerMid or (int)eCharacterClass.MaulerAlb } player)
            {
                return false;
            }

            if (!base.OnDirectEffect(target, effectiveness)) return false;
            double heal = Spell.Value * effectiveness;
            if (heal < 0) target.Mana += (int)(-heal * target.MaxMana / 100);
            else target.Mana += (int)heal;
            //"You feel calm and healthy."
            MessageToLiving(target, Spell.Message1, eChatType.CT_Spell);
            return true;
        }

        /// <summary>
        /// When an applied effect expires.
        /// Duration spells only.
        /// </summary>
        /// <param name="effect">The expired effect</param>
        /// <param name="noMessages">true, when no messages should be sent to player and surrounding</param>
        /// <returns>immunity duration in milliseconds</returns>
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            base.OnEffectExpires(effect, noMessages);
            if (!noMessages)
            {
                //"Your meditative state fades."
                MessageToLiving(effect.Owner, Spell.Message3, eChatType.CT_SpellExpires);
                //"{0}'s meditative state fades."
                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(effect.Owner == player))
                    {
                        player.MessageFromArea(effect.Owner, Util.MakeSentence(Spell.Message4,
                            player.GetPersonalizedName(effect.Owner)), eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            return 0;
        }

        // constructor
        public PoTSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Perfector.PoT.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    #region CCResist
    [SpellHandler("CCResist")]
    public class CCResistSpellHandler : MasterlevelHandling
    {
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.MesmerizeDuration] += (int)m_spell.Value;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.StunDuration] += (int)m_spell.Value;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.SpeedDecreaseDuration] += (int)m_spell.Value;

            if (effect.Owner is GamePlayer)
            {
                GamePlayer player = effect.Owner as GamePlayer;
                player!.UpdatePlayerStatus();
                player.Out.SendUpdatePlayer();
            }
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.MesmerizeDuration] -= (int)m_spell.Value;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.StunDuration] -= (int)m_spell.Value;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.SpeedDecreaseDuration] -= (int)m_spell.Value;

            if (effect.Owner is GamePlayer)
            {
                GamePlayer player = effect.Owner as GamePlayer;
                player!.UpdatePlayerStatus();
                player.Out.SendUpdatePlayer();
            }
            return base.OnEffectExpires(effect, noMessages);
        }

        // constructor
        public CCResistSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Perfector.CCResist.MainDescription", Spell.Value);

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
}
