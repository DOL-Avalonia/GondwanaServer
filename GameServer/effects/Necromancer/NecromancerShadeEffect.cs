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
using System.Collections.Generic;
using DOL.AI.Brain;

namespace DOL.GS.Effects
{
    /// <summary>
    /// Necromancer shade effect (grants immunity to all forms of
    /// attack).
    /// </summary>
    /// <author>Aredhel</author>
    public class NecromancerShadeEffect : ShadeEffect
    {
        public override void Start(GameLiving living)
        {
            base.Start(living);
            GamePlayer player = living as GamePlayer;

            if (player!.ControlledBrain != null && player.ControlledBrain.Body != null)
            {
                GameNPC pet = player.ControlledBrain.Body;
                List<GameObject> attackerList;
                lock (player.Attackers)
                    attackerList = new List<GameObject>(player.Attackers);

                foreach (GameObject obj in attackerList)
                {
                    if (obj is GameNPC)
                    {
                        GameNPC npc = (GameNPC)obj;
                        if (npc.TargetObject == player && npc.AttackState)
                        {
                            IOldAggressiveBrain brain = npc.Brain as IOldAggressiveBrain;
                            if (brain != null)
                            {
                                npc.AddAttacker(pet);
                                npc.StopAttack();
                                brain.AddToAggroList(pet, (int)(brain.GetAggroAmountForLiving(player) + 1));
                            }
                        }
                    }
                }
            }
        }

        public override bool Cancel(bool playerCanceled, bool force = false)
        {
            GamePlayer player = Owner as GamePlayer;

            if (player != null && player.ControlledBrain != null)
            {
                (player.ControlledBrain as ControlledNpcBrain)?.Stop();
            }
            return base.Cancel(playerCanceled, force);
        }

        protected int m_timeRemaining = -1;

        public override int RemainingTime
        {
            get { return (m_timeRemaining < 0) ? 0 : m_timeRemaining * 1000; }
        }

        public void SetTetherTimer(int seconds)
        {
            m_timeRemaining = seconds;
        }
    }
}
