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
using DOL.GS.Effects;
using DOL.Language;
using DOL.GS.ServerProperties;
namespace DOL.GS.Spells
{
    /// <summary>
    /// 
    /// </summary>
    [SpellHandlerAttribute("BainsheePulseDmg")]
    public class BainsheePulseDmgSpellHandler : SpellHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        public const string FOCUS_WEAK = "FocusSpellHandler.Online";
        /// <summary>
        /// Execute direct damage spell
        /// </summary>
        /// <param name="target"></param>
        /// <param name="force"></param>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            if (Spell.Pulse != 0)
            {
                GameEventMgr.AddHandler(Caster, GamePlayerEvent.Moving, new DOLEventHandler(EventAction));
                GameEventMgr.AddHandler(Caster, GamePlayerEvent.Dying, new DOLEventHandler(EventAction));
            }
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        public override PulsingSpellEffect? CancelPulsingSpell(GameLiving living, string spellType)
        {
            lock (living.ConcentrationEffects)
            {
                for (int i = 0; i < living.ConcentrationEffects.Count; i++)
                {
                    PulsingSpellEffect effect = living.ConcentrationEffects[i] as PulsingSpellEffect;
                    if (effect == null)
                        continue;
                    if (effect.SpellHandler.Spell.SpellType == spellType)
                    {
                        effect.Cancel(false);
                        GameEventMgr.RemoveHandler(Caster, GamePlayerEvent.Moving, new DOLEventHandler(EventAction));
                        GameEventMgr.RemoveHandler(Caster, GamePlayerEvent.Dying, new DOLEventHandler(EventAction));
                        return effect;
                    }
                }
            }
            return null;
        }
        public void EventAction(DOLEvent e, object sender, EventArgs args)
        {
            GameLiving player = sender as GameLiving;

            if (player == null) return;
            if (Spell.Pulse != 0 && CancelPulsingSpell(Caster, Spell.SpellType) != null)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CancelEffect"), eChatType.CT_Spell);
                return;
            }
        }

        #region LOS on Keeps

        private const string LOSEFFECTIVENESS = "LOS Effectivness";

        /// <summary>
        /// execute direct effect
        /// </summary>
        /// <param name="target">target that gets the damage</param>
        /// <param name="effectiveness">factor from 0..1 (0%-100%)</param>
        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target == null) return false;

            bool spellOK = !(Spell.Target == "Frontal" ||
                //pbaoe
                Spell is { Target: "enemy", Radius: > 0, Range: 0 });
            //cone spells

            if (!spellOK || CheckLOS(Caster))
            {
                GamePlayer player = target.GetController() as GamePlayer ?? Caster.GetController() as GamePlayer;
                if (player != null)
                {
                    player.TempProperties.setProperty(LOSEFFECTIVENESS, effectiveness);
                    player.Out.SendCheckLOS(Caster, target, new CheckLOSResponse(DealDamageCheckLOS));
                }
                else
                    DealDamage(target, effectiveness);
            }
            else DealDamage(target, effectiveness);
            return true;
        }

        private bool CheckLOS(GameLiving living)
        {
            foreach (AbstractArea area in living.CurrentAreas)
            {
                if (area.CheckLOS)
                    return true;
            }
            return false;
        }

        private void DealDamageCheckLOS(GamePlayer player, ushort response, ushort targetOID)
        {
            if (player == null) // Hmm
                return;
            if ((response & 0x100) == 0x100)
            {
                try
                {
                    GameLiving target = Caster.CurrentRegion.GetObject(targetOID) as GameLiving;
                    if (target != null)
                    {
                        double effectiveness = (double)player.TempProperties.getProperty<object>(LOSEFFECTIVENESS, null);
                        DealDamage(target, effectiveness);
                    }
                }
                catch (Exception e)
                {
                    if (log.IsErrorEnabled)
                        log.Error(string.Format("targetOID:{0} caster:{1} exception:{2}", targetOID, Caster, e));
                }
            }
        }

        private void DealDamage(GameLiving target, double effectiveness)
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
            if (target is GamePlayer && Caster.TempProperties.getProperty("player_in_keep_property", false))
            {
                GamePlayer player = target as GamePlayer;
                player!.Out.SendCheckLOS(Caster, player, new CheckLOSResponse(ResistSpellCheckLOS));
            }
            else SpellResisted(target);
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
        #endregion

        // constructor
        public BainsheePulseDmgSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;
            string damageTypeName = LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType);
            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.BainsheePulseDmg.MainDescription", Spell.Damage, damageTypeName);

            if (Spell.Radius > 0)
            {
                string areaDesc = LanguageMgr.GetTranslation(language, "SpellDescription.BainsheePulseDmg.AreaDescription");
                mainDesc += "\n\n" + areaDesc;
            }

            string secondaryDesc = LanguageMgr.GetTranslation(language, "SpellDescription.BainsheePulseDmg.SecondaryDescription");
            mainDesc += "\n\n" + secondaryDesc;

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}
