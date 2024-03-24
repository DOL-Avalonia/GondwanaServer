using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using GameServerScripts.Amtescripts.Managers;


namespace DOL.GS.Scripts
{
    public class MageMob : AmteMob
    {
        public MageMob()
        {
            SetOwnBrain(new MageMobBrain());
        }

        public override int MaxMana
        {
            get
            {
                return (int)(GetModified(eProperty.MaxMana) * 1.3);
            }
        }

        /// <summary>
        /// Interval for power regeneration tics
        /// </summary>
        protected override ushort PowerRegenerationPeriod
        {
            get { return (ushort)(m_powerRegenerationPeriod * 0.77); }
        }
    }
}
