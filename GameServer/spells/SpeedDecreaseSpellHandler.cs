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
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.Events;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandler("SpeedDecrease")]
    public class SpeedDecreaseSpellHandler : UnbreakableSpeedDecreaseSpellHandler
    {
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            // Check for root immunity.
            if (Spell.Value == 99 &&
                FindStaticEffectOnTarget(target, typeof(MezzRootImmunityEffect)) != null)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetImmune"), eChatType.CT_System);
                return true;
            }
            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            // Cannot apply if the effect owner has a charging effect
            if (effect.Owner.EffectList.GetOfType<ChargeEffect>() != null || effect.Owner.TempProperties.getProperty("Charging", false))
            {
                if (m_caster is GamePlayer player)
                    MessageToCaster(LanguageMgr.GetTranslation(player.Client, "SpellHandler.Target.TooFast", player.GetPersonalizedName(effect.Owner)), eChatType.CT_SpellResisted);
                else
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Target.TooFast", effect.Owner.Name), eChatType.CT_SpellResisted);
                return;
            }
            base.OnEffectStart(effect);
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttacked));
            // Cancels mezz on the effect owner, if applied
            GameSpellEffect mezz = SpellHandler.FindEffectOnTarget(effect.Owner, "Mesmerize");
            if (mezz != null)
                mezz.Cancel(false);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttacked));
            return base.OnEffectExpires(effect, noMessages);
        }

        protected virtual void OnAttacked(DOLEvent e, object sender, EventArgs arguments)
        {
            AttackedByEnemyEventArgs attackArgs = arguments as AttackedByEnemyEventArgs;
            GameLiving living = sender as GameLiving;
            if (attackArgs == null) return;
            if (living == null) return;

            switch (attackArgs.AttackData.AttackResult)
            {
                case GameLiving.eAttackResult.HitStyle:
                case GameLiving.eAttackResult.HitUnstyled:
                    GameSpellEffect effect = FindEffectOnTarget(living, this);
                    if (effect != null)
                        effect.Cancel(false);
                    break;
            }
        }

        public SpeedDecreaseSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line) { }
    }
}
