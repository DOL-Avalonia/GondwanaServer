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
using System.Collections.Generic;
using System.Numerics;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.SkillHandler;
using DOL.Language;

namespace DOL.GS.Spells
{
    /// <summary>
    /// 
    /// </summary>
    [SpellHandlerAttribute("Chamber")]
    public class ChamberSpellHandler : SpellHandler
    {
        private Spell m_primaryspell = null;
        private SpellLine m_primaryspellline = null;
        private Spell m_secondaryspell = null;
        private SpellLine m_secondaryspelline = null;
        private int m_effectslot = 0;

        public Spell PrimarySpell
        {
            get
            {
                return m_primaryspell;
            }
            set
            {
                m_primaryspell = value;
            }
        }

        public SpellLine PrimarySpellLine
        {
            get
            {
                return m_primaryspellline;
            }
            set
            {
                m_primaryspellline = value;
            }
        }

        public Spell SecondarySpell
        {
            get
            {
                return m_secondaryspell;
            }
            set
            {
                m_secondaryspell = value;
            }
        }

        public SpellLine SecondarySpellLine
        {
            get
            {
                return m_secondaryspelline;
            }
            set
            {
                m_secondaryspelline = value;
            }
        }

        public int EffectSlot
        {
            get
            {
                return m_effectslot;
            }
            set
            {
                m_effectslot = value;
            }
        }

        public override void InterruptCasting()
        {
            base.InterruptCasting();
            Caster.CurrentSpellHandler = null;
        }
        public override bool CastSpell()
        {
            GamePlayer caster = (GamePlayer)m_caster;
            m_spellTarget = caster.TargetObject as GameLiving;
            GameSpellEffect effect = SpellHandler.FindEffectOnTarget(caster, "Chamber", m_spell.Name);
            if (effect != null && m_spell.Name == effect.Spell.Name)
            {
                ISpellHandler spellhandler = null;
                ISpellHandler spellhandler2 = null;
                ChamberSpellHandler chamber = (ChamberSpellHandler)effect.SpellHandler;
                GameSpellEffect PhaseShift = SpellHandler.FindEffectOnTarget(m_spellTarget, "Phaseshift");
                SelectiveBlindnessEffect SelectiveBlindness = Caster.EffectList.GetOfType<SelectiveBlindnessEffect>();
                spellhandler = ScriptMgr.CreateSpellHandler(caster, chamber.PrimarySpell, chamber.PrimarySpellLine);

                #region Pre-checks
                int duration = caster.GetSkillDisabledDuration(m_spell);
                if (duration > 0)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.MustWaitBeforeUse", (duration / 1000 + 1)), eChatType.CT_System);
                    return false;
                }
                if (caster.IsMoving || caster.IsStrafing)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.CantCastWhileMoving"), eChatType.CT_SpellResisted);
                    return false;
                }
                if (caster.IsSitting)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.CantCastWhileSitting2"), eChatType.CT_SpellResisted);
                    return false;
                }
                if (m_spellTarget == null)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.MustSelectTarget"), eChatType.CT_SpellResisted);
                    return false;
                }
                if (!caster.IsAlive)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.DeadCantCast"), eChatType.CT_SpellResisted);
                    return false;
                }
                if (!m_spellTarget.IsAlive)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.CantCastOnDead"), eChatType.CT_SpellResisted);
                    return false;
                }
                if (caster.IsMezzed || caster.IsStunned || caster.IsSilenced)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.Chamber.CannotUseState"), eChatType.CT_System);
                    return false;
                }
                if (!caster.TargetInView)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.TargetNotVisible"), eChatType.CT_SpellResisted);
                    return false;
                }
                if (caster.IsObjectInFront(m_spellTarget, 180) == false)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.TargetNotInView"), eChatType.CT_SpellResisted);
                    return false;
                }
                if (caster.IsInvulnerableToAttack)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.ChamberSpell.YouInvulnerable"), eChatType.CT_System);
                    return false;
                }
                if (m_spellTarget is GamePlayer)
                {
                    if ((m_spellTarget as GamePlayer)!.IsInvulnerableToAttack)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.ChamberSpell.TargetInvulnerable"), eChatType.CT_System);
                        return false;
                    }
                }
                if (!caster.IsWithinRadius(m_spellTarget, ((SpellHandler)spellhandler).CalculateSpellRange()))
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.TargetTooFar"), eChatType.CT_SpellResisted);
                    return false;
                }
                if (PhaseShift != null)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "Skill.Ability.TargetIsPhaseshifted", Caster.GetPersonalizedName(m_spellTarget)), eChatType.CT_System);
                    return false;
                }
                if (SelectiveBlindness != null)
                {
                    GameLiving EffectOwner = SelectiveBlindness.EffectSource;
                    if (EffectOwner == m_spellTarget)
                    {
                        GamePlayer player = m_caster as GamePlayer;
                        if (player != null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameLiving.AttackData.InvisibleToYou", m_spellTarget.GetName(0, true)), eChatType.CT_Missed, eChatLoc.CL_SystemWindow);
                        }
                        return false;
                    }
                }
                if (m_spellTarget.HasAbility(Abilities.DamageImmunity))
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.DamageImmunity", Caster.GetPersonalizedName(m_spellTarget)), eChatType.CT_SpellResisted);
                    return false;
                }
                if (GameServer.ServerRules.IsAllowedToAttack(Caster, m_spellTarget, true) && chamber.PrimarySpell.Target.ToLower() == "realm")
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.OnlyWorksOnFriendlyTargets"), eChatType.CT_SpellResisted);
                    return false;
                }
                if (!GameServer.ServerRules.IsAllowedToAttack(Caster, m_spellTarget, true) && chamber.PrimarySpell.Target.ToLower() != "realm")
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.Chamber.TargetNotAttackable"), eChatType.CT_System);
                    return false;
                }
                spellhandler.CastSpell();
                #endregion

                if (chamber.SecondarySpell != null)
                {
                    spellhandler2 = ScriptMgr.CreateSpellHandler(caster, chamber.SecondarySpell, chamber.SecondarySpellLine);
                    spellhandler2.CastSpell();
                }
                effect.Cancel(false);

                if (m_caster is GamePlayer)
                {
                    GamePlayer player_Caster = Caster as GamePlayer;
                    foreach (SpellLine spellline in player_Caster!.GetSpellLines())
                        foreach (Spell sp in SkillBase.GetSpellList(spellline.KeyName))
                            if (sp.SpellType == m_spell.SpellType)
                                m_caster.DisableSkill(sp, sp.RecastDelay);
                }
                else if (m_caster is GameNPC)
                    m_caster.DisableSkill(m_spell, m_spell.RecastDelay);
            }
            else
            {
                base.CastSpell();
                int duration = caster.GetSkillDisabledDuration(m_spell);
                if (Caster is GamePlayer && duration == 0)
                {
                    ((GamePlayer)Caster).Out.SendMessage(LanguageMgr.GetTranslation(((GamePlayer)Caster).Client, "SpellHandler.Chamber.SelectFirstSpell", Spell.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
            return true;
        }

        /// <summary>
        /// Fire bolt
        /// </summary>
        /// <param name="target"></param>
        /// <param name="force"></param>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            ConsumePower(target);

            // endurance
            m_caster.Endurance -= 5;

            // messages
            GamePlayer caster = (GamePlayer)m_caster;
            if (Spell.InstrumentRequirement == 0)
            {
                if (SecondarySpell == null && PrimarySpell == null)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.Chamber.NoSpellsLoaded", m_spell.Name), eChatType.CT_Spell);
                }
                else
                {
                    MessageToCaster(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.Chamber.ReadyForUse", m_spell.Name), eChatType.CT_Spell);
                    //StartSpell(target); // and action
                    GameSpellEffect neweffect = CreateSpellEffect(target, 1);
                    neweffect.Start(m_caster);
                    SendEffectAnimation(m_caster, 0, false, 1);
                    ((GamePlayer)m_caster).Out.SendWarlockChamberEffect((GamePlayer)m_caster);
                }

                foreach (GamePlayer player in m_caster.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (player != m_caster)
                        player.MessageFromArea(m_caster, LanguageMgr.GetTranslation(player.Client, "SpellHandler.Chamber.CastsSpell", player.GetPersonalizedName(m_caster)), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }

            //the quick cast is unallowed whenever you miss the spell
            //set the time when casting to can not quickcast during a minimum time
            QuickCastEffect quickcast = m_caster.EffectList.GetOfType<QuickCastEffect>();
            if (quickcast != null && Spell.CastTime > 0)
            {
                m_caster.TempProperties.setProperty(GamePlayer.QUICK_CAST_CHANGE_TICK, m_caster.CurrentRegion.Time);
                m_caster.DisableSkill(SkillBase.GetAbility(Abilities.Quickcast), QuickCastAbilityHandler.DISABLE_DURATION);
                quickcast.Cancel(false);
            }
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {

            ((GamePlayer)m_caster).Out.SendWarlockChamberEffect((GamePlayer)effect.Owner);
            return base.OnEffectExpires(effect, noMessages);
        }

        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            return new GameSpellEffect(this, 0, 0, effectiveness);
        }

        public static int GetEffectSlot(string spellName)
        {
            switch (spellName)
            {
                case "Chamber of Minor Fate":
                    return 1;
                case "Chamber of Restraint":
                    return 2;
                case "Chamber of Destruction":
                    return 3;
                case "Chamber of Fate":
                    return 4;
                case "Chamber of Greater Fate":
                    return 5;
                case "Chamber of Creation":
                    return 6;
            }

            return 0;
        }
        #region Devle Info
        public override IList<string> DelveInfo
        {
            get
            {
                var list = new List<string>();

                //Name
                list.Add("Name: " + Spell.Name);
                list.Add("");

                //Description
                list.Add("Description: " + Spell.Description);
                list.Add("");

                //SpellType
                if (!Spell.AllowBolt)
                    list.Add("Type: Any but bolts");
                if (Spell.AllowBolt)
                    list.Add("Type: Any");

                //Cast
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.CastingTime", (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'")));
                //Recast
                if (Spell.RecastDelay > 60000)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.RecastTime") + (Spell.RecastDelay / 60000).ToString() + ":" + (Spell.RecastDelay % 60000 / 1000).ToString("00") + " min");
                else if (Spell.RecastDelay > 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.RecastTime") + (Spell.RecastDelay / 1000).ToString() + " sec");
                return list;
            }
        }
        #endregion
        // constructor
        public ChamberSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string description = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Chamber.MainDescription");

            if (!Spell.AllowBolt)
            {
                string cannotContainBolts = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Chamber.CannotContainBolts");
                description += "\n\n" + cannotContainBolts;
            }
            else
            {
                string canContainBolts = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Chamber.CanContainBolts");
                description += "\n\n" + canContainBolts;
            }

            return description;
        }
    }
}
