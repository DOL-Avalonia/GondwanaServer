using System;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Geometry;

namespace AmteScripts.PvP.CTF
{
    public class GameFlagBasePad : GameStaticItem
    {
        public int FlagID { get; set; }
        public bool IsEmpty { get; private set; } = true;
        public GameFlagStatic CurrentFlag { get; private set; }

        public override bool AddToWorld()
        {
            bool result = base.AddToWorld();
            if (result)
            {
                SpawnFlag();
            }
            return result;
        }

        public void SpawnFlag()
        {
            if (!IsEmpty) return;
            if (ObjectState != eObjectState.Active) return;
            var flag = new GameFlagStatic(this);
            flag.Model = 3223;
            flag.Name = "Capture Flag #" + FlagID;
            flag.Level = 50;
            flag.Realm = 0;
            flag.Position = Position.Create(CurrentRegionID, Position.X, Position.Y, Position.Z, Heading);
            flag.AddToWorld();
            CurrentFlag = flag;
            IsEmpty = false;
        }

        public void RemoveFlag()
        {
            if (CurrentFlag != null && CurrentFlag.ObjectState == eObjectState.Active)
            {
                CurrentFlag.RemoveFromWorld();
                CurrentFlag = null;
            }
            IsEmpty = true;
        }

        /// <summary>
        /// Called by the flag itself if the flag despawns or is removed from this pad
        /// </summary>
        public void NotifyFlagLeft()
        {
            CurrentFlag = null;
            IsEmpty = true;
        }

        /// <summary>
        /// If the flag drops on the ground and times out, it should respawn back here.
        /// </summary>
        public void RespawnFlag()
        {
            if (CurrentFlag != null)
            {
                CurrentFlag.RemoveFromWorld();
                CurrentFlag = null;
            }
            IsEmpty = true;
            SpawnFlag();
        }
    }
}