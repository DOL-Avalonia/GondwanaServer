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
using System.Linq;

using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandlerAttribute("Fear")]
    public class FearSpellHandler : SpellHandler
    {
        /// <summary>
        /// Dictionary to Keep Track of Fear Brains attached to NPCs
        /// </summary>
        private readonly ReaderWriterDictionary<GameNPC, FearBrain> m_NPCFearBrains = new ReaderWriterDictionary<GameNPC, FearBrain>();

        private static bool HasEffect(GameLiving target, string spellType)
            => SpellHandler.FindEffectOnTarget(target, spellType) != null;

        /// <summary>
        /// Is the NPC currently charmed or befriended?
        /// </summary>
        private static bool IsCharmedOrFriend(GameNPC npc)
            => HasEffect(npc, "Charm") || HasEffect(npc, "BeFriend") || (npc.Brain is FriendBrain);

        /// <summary>
        /// Consume Power on Spell Start
        /// </summary>
        /// <param name="target"></param>
        /// <param name="force"></param>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        /// <summary>
        /// Select only GameNPC Targets
        /// </summary>
        /// <param name="castTarget"></param>
        /// <returns></returns>
        public override IList<GameLiving> SelectTargets(GameObject castTarget, bool force = false)
        {
            return base.SelectTargets(castTarget, force).Where(t => t is GameNPC).ToList();
        }

        /// <summary>
        /// called when spell effect has to be started and applied to targets
        /// </summary>
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            var npcTarget = target as GameNPC;
            if (npcTarget == null) return false;

            if (npcTarget.Brain is ControlledNpcBrain && !IsCharmedOrFriend(npcTarget) && npcTarget.Brain is not FollowingFriendMobBrain && npcTarget.Brain is not IllusionPetBrain)
            {
                return false;
            }

            if (npcTarget.Brain is IllusionPetBrain)
            {
                if (npcTarget.IsAlive)
                    npcTarget.Die(m_caster);
                return true;
            }

            if (npcTarget.Level > Spell.Value)
            {
                // Resisted
                OnSpellResisted(target);
                return true;
            }

            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        /// <summary>
        /// On Effect Start Replace Brain with Fear Brain.
        /// </summary>
        /// <param name="effect"></param>
        public override void OnEffectStart(GameSpellEffect effect)
        {
            var npcTarget = effect.Owner as GameNPC;

            var fearBrain = new FearBrain();
            m_NPCFearBrains.AddOrReplace(npcTarget, fearBrain);

            npcTarget!.AddBrain(fearBrain);
            fearBrain.Think();

            base.OnEffectStart(effect);
        }

        /// <summary>
        /// Called when Effect Expires
        /// </summary>
        /// <param name="effect"></param>
        /// <param name="noMessages"></param>
        /// <returns></returns>
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            var npcTarget = effect.Owner as GameNPC;

            FearBrain fearBrain;
            if (m_NPCFearBrains.TryRemove(npcTarget, out fearBrain))
            {
                fearBrain.RemoveEffect();
                npcTarget!.RemoveBrain(fearBrain);
            }

            if (npcTarget!.Brain == null)
                npcTarget.AddBrain(new StandardMobBrain());

            return base.OnEffectExpires(effect, noMessages);
        }

        /// <summary>
        /// Spell Resists don't trigger notification or interrupt
        /// </summary>
        /// <param name="target"></param>
        public override void OnSpellResisted(GameLiving target)
        {
            SendSpellResistAnimation(target);
            SendSpellResistMessages(target);
            StartSpellResistLastAttackTimer(target);
        }

        /// <summary>
        /// Default Constructor
        /// </summary>
        /// <param name="caster"></param>
        /// <param name="spell"></param>
        /// <param name="line"></param>
        public FearSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;
            string mainDesc;

            if (Spell.Radius > 0)
            {
                mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Fear.AreaTarget", Spell.Value, Spell.Radius);
            }
            else
            {
                mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Fear.SingleTarget", Spell.Value);
            }

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}
