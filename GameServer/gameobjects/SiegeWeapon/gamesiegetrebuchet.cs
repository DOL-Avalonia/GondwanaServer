using System;

namespace DOL.GS
{
    /// <summary>
    /// GameMovingObject is a base class for boats and siege weapons.
    /// </summary>
    public class GameSiegeTrebuchet : GameSiegeCatapult
    {

        public GameSiegeTrebuchet()
            : base()
        {
            MeleeDamageType = eDamageType.Crush;
            Name = "trebuchet";
            AmmoType = 0x3A;
            EnableToMove = false;
            this.Model = 0xA2E;
            this.Effect = 0x89C;
            ActionDelay = new int[]
            {
                0,//none
				5000,//aiming
				15000,//arming
				0,//loading
				4500//fireing
			};//en ms
        }
    }
}
