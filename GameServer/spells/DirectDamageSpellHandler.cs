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
using System.Collections;
using DOL.AI.Brain;
using DOL.GS.PacketHandler;
using DOL.GS.Keeps;
using DOL.Events;
using System.Collections.Generic;
using DOL.GS.PlayerClass;
using DOL.Language;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    [SpellHandlerAttribute("DirectDamage")]
    public class DirectDamageSpellHandler : SpellHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        private bool m_castFailed = false;

        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            if (!m_castFailed)
            {
                m_caster.Mana -= CalculatePowerCost(target);
            }

            base.FinishSpellCast(target, force);
        }

        private const string LOSEFFECTIVENESS = "LOS Effectivness";

        public override double CalculateDamageBase(GameLiving target)
        {
            // % damage procs
            if (Spell.Damage < 0)
            {
                double spellDamage = 0;

                // This equation is used to simulate live values - Tolakram
                spellDamage = (target.MaxHealth * -Spell.Damage * .01) / 2.5;

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

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target == null) return false;

            bool spellOK = true;

            if (Spell.Target.ToLower() == "cone" || (Spell.Target == "enemy" && Spell.Radius > 0 && Spell.Range == 0))
            {
                spellOK = false;
            }

            if (spellOK == false || MustCheckLOS(Caster))
            {
                GamePlayer checkPlayer = target.GetController() as GamePlayer ?? Caster.GetController() as GamePlayer;
                if (checkPlayer != null)
                {
                    checkPlayer.TempProperties.setProperty(LOSEFFECTIVENESS + target.ObjectID, effectiveness);
                    checkPlayer.Out.SendCheckLOS(Caster, target, new CheckLOSResponse(DealDamageCheckLOS));
                }
                else
                {
                    DealDamage(target, effectiveness);
                }
            }
            else
            {
                DealDamage(target, effectiveness);
            }
            return true;
        }

        protected virtual void DealDamageCheckLOS(GamePlayer player, ushort response, ushort targetOID)
        {
            if (player == null || Caster.ObjectState != GameObject.eObjectState.Active)
                return;

            if ((response & 0x100) == 0x100)
            {
                try
                {
                    GameLiving target = Caster.CurrentRegion.GetObject(targetOID) as GameLiving;
                    if (target != null)
                    {
                        double effectiveness = player.TempProperties.getProperty<double>(LOSEFFECTIVENESS + target.ObjectID, 1.0);
                        DealDamage(target, effectiveness);
                        player.TempProperties.removeProperty(LOSEFFECTIVENESS + target.ObjectID);
                        // Due to LOS check delay the actual cast happens after FinishSpellCast does a notify, so we notify again
                        GameEventMgr.Notify(GameLivingEvent.CastFinished, m_caster, new CastingEventArgs(this, target, m_lastAttackData));
                    }
                }
                catch (Exception e)
                {
                    m_castFailed = true;

                    if (log.IsErrorEnabled)
                        log.Error(string.Format("targetOID:{0} caster:{1} exception:{2}", targetOID, Caster, e));
                }
            }
            else
            {
                if (Spell.Target.ToLower() == "enemy" && Spell.Radius == 0 && Spell.Range != 0)
                {
                    m_castFailed = true;
                    MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.CheckLOSPlayerToTarget.CantSeeTarget"), eChatType.CT_SpellResisted);
                }
            }
        }

        protected virtual void DealDamage(GameLiving target, double effectiveness)
        {
            if (!target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active) return;

            // calc damage
            AttackData ad = CalculateDamageToTarget(target, effectiveness);

            // Attacked living may modify the attack data.
            ad.Target.ModifyAttack(ad);

            SendDamageMessages(ad);
            DamageTarget(ad, true);
            target.StartInterruptTimer(target.SpellInterruptDuration, ad.AttackType, Caster);
        }

        /*
		 * We need to send resist spell los check packets because spell resist is calculated first, and
		 * so you could be inside keep and resist the spell and be interupted when not in view
		 */
        public override void OnSpellResisted(GameLiving target)
        {
            if (target is GamePlayer)
            {
                GamePlayer player = target as GamePlayer;
                player!.Out.SendCheckLOS(Caster, player, new CheckLOSResponse(ResistSpellCheckLOS));
            }
            else
            {
                SpellResisted(target);
            }
        }

        private void ResistSpellCheckLOS(GamePlayer player, ushort response, ushort targetOID)
        {
            if ((response & 0x100) == 0x100)
            {
                try
                {
                    GameLiving target = Caster.CurrentRegion.GetObject(targetOID) as GameLiving;
                    if (target != null)
                        SpellResisted(target);
                }
                catch (Exception e)
                {
                    if (log.IsErrorEnabled)
                        log.Error(string.Format("targetOID:{0} caster:{1} exception:{2}", targetOID, Caster, e));
                }
            }
        }

        private void SpellResisted(GameLiving target)
        {
            base.OnSpellResisted(target);
        }

        public DirectDamageSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            string description = LanguageMgr.GetTranslation(language, "SpellDescription.DirectDamage.MainDescription", Spell.Damage, LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType));

            if (Spell.SubSpellID != 0)
            {
                Spell subSpell = SkillBase.GetSpellByID((int)Spell.SubSpellID);
                if (subSpell != null)
                {
                    ISpellHandler subSpellHandler = ScriptMgr.CreateSpellHandler(m_caster, subSpell, null);
                    if (subSpellHandler != null)
                    {
                        string subspelldesc = subSpellHandler.GetDelveDescription(delveClient);
                        description += "\n\n" + subspelldesc;
                    }
                }
            }

            if (Spell.IsSecondary)
            {
                string secondaryMessage = LanguageMgr.GetTranslation(language, "SpellDescription.Warlock.SecondarySpell");
                description += "\n\n" + secondaryMessage;
            }

            if (Spell.IsPrimary)
            {
                string secondaryMessage = LanguageMgr.GetTranslation(language, "SpellDescription.Warlock.PrimarySpell");
                description += "\n\n" + secondaryMessage;
            }

            switch (Spell.ID)
            {
                case 25075:
                case 25076:
                case 25077:
                case 25078:
                case 25079:
                    {
                        var viewer = delveClient?.Player;
                        if (viewer?.CharacterClass is ClassOccultist)
                        {
                            description += "\n\n" + LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.ConditionDescription1");
                        }
                        break;
                    }
            }

            return description;
        }
    }
}
