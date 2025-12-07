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

using DOL.GS;
using DOL.GS.Spells;
using DOL.GS.PacketHandler;
using DOL.Events;
using DOL.GS.Effects;
using DOL.AI.Brain;
using DOL.GS.PlayerClass;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// The necromancer character class.
    /// </summary>
    public class CharacterClassNecromancer : ClassDisciple
    {
        public override void Init(GamePlayer player)
        {
            base.Init(player);

            // Force caster form when creating this player in the world.
            player.Model = (ushort)player.Client.Account.Characters[player.Client.ActiveCharIndex].CreationModel;
            Shade(false);
        }


        //private String m_petName = "";
        private int m_savedPetHealthPercent = 0;

        /// <summary>
        /// Sets the controlled object for this player
        /// </summary>
        /// <param name="controlledNpc"></param>
        public override void SetControlledBrain(IControlledBrain controlledNpcBrain)
        {
            m_savedPetHealthPercent = (Player.ControlledBrain != null)
                ? (int)Player.ControlledBrain.Body.HealthPercent : 0;

            base.SetControlledBrain(controlledNpcBrain);

            if (controlledNpcBrain == null)
            {
                OnPetReleased();
            }
        }

        /// <summary>
        /// Releases controlled object
        /// </summary>
        public override void CommandNpcRelease()
        {
            m_savedPetHealthPercent = (Player.ControlledBrain != null) ? (int)Player.ControlledBrain.Body.HealthPercent : 0;

            base.CommandNpcRelease();
            OnPetReleased();
        }

        /// <summary>
        /// Invoked when pet is released.
        /// </summary>
        public override void OnPetReleased()
        {
            Shade(false);

            Player.InitControlledBrainArray(0);
        }

        /// <summary>
        /// Necromancer can only attack when it's not a shade.
        /// </summary>
        /// <param name="attackTarget"></param>
        public override bool StartAttack(GameObject attackTarget)
        {
            if (!Player.IsShade)
            {
                return true;
            }
            else
            {
                Player.Out.SendMessage("You cannot enter combat while in shade form!", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return false;
            }
        }

        /// <summary>
        /// If the pet is up, show the pet's health in the group window.
        /// </summary>
        public override byte HealthPercentGroupWindow
        {
            get
            {
                if (Player.ControlledBrain == null)
                    return Player.HealthPercent;

                return Player.ControlledBrain.Body.HealthPercent;
            }
        }

        /// <summary>
        /// Create a necromancer shade effect for this player.
        /// </summary>
        /// <returns></returns>
        public override ShadeEffect CreateShadeEffect()
        {
            return new NecromancerShadeEffect();
        }

        /// <inheritdoc />
        public override bool EnterShade(bool quiet = false)
        {
            if (!base.EnterShade(quiet))
                return false;

            if (Player.ControlledBrain is not { Body: not null })
                return false;
            
            // Necromancer has become a shade. Have any previous NPC 
            // attackers aggro on pet now, as they can't attack the 
            // necromancer any longer.
            GameNPC pet = Player.ControlledBrain.Body;
            List<GameObject> attackerList;
            lock (Player.Attackers)
                attackerList = new List<GameObject>(Player.Attackers);

            foreach (GameNPC npc in attackerList.OfType<GameNPC>())
            {
                if (npc.TargetObject != Player || !npc.AttackState)
                    continue;

                if (npc.Brain is IOldAggressiveBrain brain)
                {
                    npc.AddAttacker(pet);
                    npc.StopAttack();
                    brain.AddToAggroList(pet, (int)(brain.GetAggroAmountForLiving(Player) + 1));
                }
            }
            return true;
        }

        /// <inheritdoc />
        public override bool LeaveShade()
        {
            if (!base.LeaveShade())
                return false;
            
            // Necromancer has lost shade form, release the pet if it
            // isn't dead already and update necromancer's current health.

            if (Player.ControlledBrain is ControlledNpcBrain petBrain)
                petBrain.Stop();

            Player.Health = Math.Min(Player.Health, Player.MaxHealth * Math.Max(10, m_savedPetHealthPercent) / 100);
            return true;
        }

        /// <summary>
        /// Called when player is removed from world.
        /// </summary>
        /// <returns></returns>
        public override bool RemoveFromWorld()
        {
            // Force caster form.
            if (Player.IsShade)
                Shade(false);

            return base.RemoveFromWorld();
        }

        /// <summary>
        /// Drop shade first, this in turn will release the pet.
        /// </summary>
        /// <param name="killer"></param>
        public override void Die(GameObject killer)
        {
            if (Player.IsShade)
                Shade(false);
            base.Die(killer);
        }

        public override void Notify(DOLEvent e, object sender, EventArgs args)
        {
            if (Player.ControlledBrain != null)
            {
                GameNPC pet = Player.ControlledBrain.Body;

                if (pet != null && sender == pet && e == GameLivingEvent.CastStarting && args is CastingEventArgs)
                {
                    ISpellHandler spellHandler = (args as CastingEventArgs).SpellHandler;

                    if (spellHandler != null)
                    {
                        int powerCost = spellHandler.CalculatePowerCost(Player);

                        if (powerCost > 0)
                            Player.ChangeMana(Player, GameLiving.eManaChangeType.Spell, -powerCost);
                    }

                    return;
                }
            }

            base.Notify(e, sender, args);
        }
    }
}
