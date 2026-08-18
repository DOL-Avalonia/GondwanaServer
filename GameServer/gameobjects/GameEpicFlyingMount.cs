using System;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.GS.Geometry;
using System.Linq;

namespace DOL.GS.Scripts
{
    public class GameEpicFlyingMount : GameNPC
    {
        public GamePlayer Owner { get; set; }

        private RegionTimer m_checkTimer;
        private RegionTimer m_zTimer;
        private RegionTimer m_flightTimer;

        private int m_zTarget;
        private int m_zStep;
        private int m_targetDestX;
        private int m_targetDestY;

        public bool IsCruising { get; set; } = false;
        public bool IsMovingToTarget { get; set; } = false;

        public GameEpicFlyingMount() : base()
        {
            Realm = eRealm.None;
            Flags = eFlags.PEACE | eFlags.FLYING | eFlags.DONTSHOWNAME;
            MaxSpeedBase = 500;
            Level = 50;
            Size = 65;
            SetOwnBrain(new BlankBrain());
        }

        public override int MAX_PASSENGERS { get { return 1; } }
        public override int SLOT_OFFSET { get { return 0; } }

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;

            m_zTarget = this.Position.Z;
            m_checkTimer = new RegionTimer(this, new RegionTimerCallback(CheckDistanceCallback), 2000);
            m_flightTimer = new RegionTimer(this, new RegionTimerCallback(MovementTickCallback), 500);

            return true;
        }

        public override bool RemoveFromWorld()
        {
            if (m_checkTimer != null) { m_checkTimer.Stop(); m_checkTimer = null; }
            if (m_zTimer != null) { m_zTimer.Stop(); m_zTimer = null; }
            if (m_flightTimer != null) { m_flightTimer.Stop(); m_flightTimer = null; }
            return base.RemoveFromWorld();
        }

        private void HandleOutOfBounds()
        {
            IsCruising = false;
            IsMovingToTarget = false;
            this.StopMoving();

            GamePlayer rider = Owner;
            if (rider != null)
            {
                rider.Out.SendMessage(LanguageMgr.GetTranslation(rider.Client.Account.Language, "EpicMount.Msg.OutOfBounds"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                this.RiderDismount(true, rider);
                var backward = Vector.Create(this.Orientation + Angle.Degrees(180), length: 150);
                rider.MoveTo(rider.Position + backward);
            }
            this.Delete();
        }

        protected virtual int MovementTickCallback(RegionTimer timer)
        {
            if (Owner == null || RiderArrayLocation(Owner) == -1)
            {
                IsCruising = false;
                IsMovingToTarget = false;
                return 500;
            }

            if (this.CurrentZone == null)
            {
                HandleOutOfBounds();
                return 0;
            }

            int waterLvl = CurrentZone != null ? CurrentZone.Waterlevel : 0;
            if (this.IsUnderwater || (waterLvl != 0 && this.Position.Z <= waterLvl))
            {
                Owner.Out.SendMessage(LanguageMgr.GetTranslation(Owner.Client.Account.Language, "EpicMount.Msg.NoWater"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                this.RiderDismount(true, Owner);
                Delete();
                return 0;
            }

            if (Owner.Steed == this && Owner.Position.Z != this.Position.Z)
            {
                Owner.Position = Position.Create(Owner.CurrentRegionID, Owner.Position.X, Owner.Position.Y, this.Position.Z, Owner.Position.Orientation.InHeading);
            }

            if (IsCruising)
            {
                if (Owner.Orientation.InHeading != this.Orientation.InHeading)
                {
                    this.Orientation = Owner.Orientation;
                }

                var forward = Vector.Create(this.Orientation, length: MaxSpeedBase);
                var destPos = this.Position + forward;
                Coordinate dest = Coordinate.Create((int)destPos.X, (int)destPos.Y, this.Position.Z);
                this.WalkTo(dest, (short)MaxSpeedBase, true);
            }
            else if (IsMovingToTarget)
            {
                Coordinate targetXY = Coordinate.Create(m_targetDestX, m_targetDestY, this.Position.Z);

                // If close enough to target XY, stop
                if (this.Coordinate.DistanceTo(targetXY, true) < 150)
                {
                    IsMovingToTarget = false;
                    this.StopMoving();
                    Owner.Out.SendMessage(LanguageMgr.GetTranslation(Owner.Client.Account.Language, "EpicMount.Msg.Arrived"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    this.TurnTo(targetXY, true);
                    var forward = Vector.Create(this.Orientation, length: MaxSpeedBase);
                    var destPos = this.Position + forward;
                    Coordinate dest = Coordinate.Create((int)destPos.X, (int)destPos.Y, this.Position.Z);
                    this.WalkTo(dest, (short)MaxSpeedBase, true);
                }
            }

            return 500;
        }

        public void Rotate(int degrees)
        {
            Angle diff = Angle.Degrees(degrees);
            this.Orientation += diff;
            if (Owner != null)
            {
                Owner.Orientation += diff;
            }

            if (IsCruising)
            {
                var forward = Vector.Create(this.Orientation, length: MaxSpeedBase);
                var destPos = this.Position + forward;
                Coordinate dest = Coordinate.Create((int)destPos.X, (int)destPos.Y, this.Position.Z);
                this.WalkTo(dest, (short)MaxSpeedBase, true);
            }
        }

        public void ToggleCruise()
        {
            if (Owner == null) return;
            string lang = Owner.Client.Account.Language;

            IsMovingToTarget = false;
            IsCruising = !IsCruising;
            if (!IsCruising)
            {
                this.StopMoving();
                Owner.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Msg.CruiseOff"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
            else
            {
                Owner.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Msg.CruiseOn"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                MovementTickCallback(null);
            }
        }

        public void MoveToGroundTarget()
        {
            if (Owner == null) return;
            string lang = Owner.Client.Account.Language;

            if (Owner.GroundTargetPosition == Position.Nowhere)
            {
                Owner.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Msg.SetGroundTarget"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            IsCruising = false;
            IsMovingToTarget = true;
            m_targetDestX = Owner.GroundTargetPosition.X;
            m_targetDestY = Owner.GroundTargetPosition.Y;
            Owner.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Msg.FlyToTarget"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        public void MoveToSelectedTarget()
        {
            if (Owner == null) return;
            string lang = Owner.Client.Account.Language;

            if (Owner.TargetObject == null)
            {
                Owner.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Msg.SelectTarget"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            IsCruising = false;
            IsMovingToTarget = true;
            m_targetDestX = Owner.TargetObject.Position.X;
            m_targetDestY = Owner.TargetObject.Position.Y;
            Owner.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Msg.FlyToPlayer"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        protected virtual int CheckDistanceCallback(RegionTimer timer)
        {
            if (Owner == null || !Owner.IsAlive || Owner.ObjectState != eObjectState.Active || Owner.CurrentRegionID != CurrentRegionID)
            {
                Delete();
                return 0;
            }

            if (Owner.CurrentZone == null)
            {
                HandleOutOfBounds();
                return 0;
            }

            if (RiderArrayLocation(Owner) == -1)
            {
                if (!Zone.CheckSquareDistance(Coordinate, Owner.Coordinate, 800 * 800, false))
                {
                    Owner.Out.SendMessage(LanguageMgr.GetTranslation(Owner.Client.Account.Language, "EpicMount.Msg.Vanishes"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    Delete();
                    return 0;
                }
            }
            return 2000;
        }

        public override bool Interact(GamePlayer player)
        {
            string lang = player.Client.Account.Language;

            if (player != Owner)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Interact.NotOwner"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (!Zone.CheckSquareDistance(Coordinate, player.Coordinate, 400 * 400, false))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Interact.TooFar"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.IsRiding && RiderSlot(player) != -1)
            {
                player.DismountSteed(true);
                return true;
            }

            if (player.IsRiding && RiderSlot(player) == -1) player.DismountSteed(true);
            if (player.IsOnHorse) player.IsOnHorse = false;

            player.MountSteed(this, true);
            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Interact.Help"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
        }

        public override bool RiderDismount(bool forced, GamePlayer player)
        {
            IsCruising = false;
            IsMovingToTarget = false;
            this.StopMoving();

            bool success = base.RiderDismount(forced, player);
            return success;
        }

        public void ChangeAltitude(int amount)
        {
            if (m_zTimer != null && m_zTimer.IsAlive || Owner == null) return;
            string lang = Owner.Client.Account.Language;

            int maxAltitude = CurrentZone != null ? CurrentZone.MaxFlyAltitude : 10000;
            int waterLevel = CurrentZone != null ? CurrentZone.Waterlevel : 0;

            m_zTarget = this.Position.Z + amount;

            if (amount <= -20000)
            {
                m_zTarget = waterLevel != 0 ? waterLevel + 10 : 0;
            }
            else if (m_zTarget > maxAltitude)
            {
                m_zTarget = maxAltitude;
                Owner.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Msg.MaxAltitude"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

            if (waterLevel != 0 && m_zTarget <= waterLevel)
            {
                m_zTarget = waterLevel + 10;
                Owner.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Msg.CantWater"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

            if (m_zTarget < 0 && waterLevel == 0) m_zTarget = 0;

            m_zStep = m_zTarget > this.Position.Z ? 10 : -10;

            m_zTimer = new RegionTimer(this, new RegionTimerCallback(ZTimerCallback), 50);
        }

        protected virtual int ZTimerCallback(RegionTimer timer)
        {
            bool reached = false;
            int newZ = this.Position.Z + m_zStep;

            if ((m_zStep > 0 && newZ >= m_zTarget) || (m_zStep < 0 && newZ <= m_zTarget))
            {
                newZ = m_zTarget;
                reached = true;
            }

            // Ensure dismount if flying down into the water
            int waterLevel = CurrentZone != null ? CurrentZone.Waterlevel : 0;
            if (waterLevel != 0 && newZ <= waterLevel)
            {
                newZ = waterLevel + 10;
                reached = true;
                if (Owner != null)
                {
                    Owner.Out.SendMessage(LanguageMgr.GetTranslation(Owner.Client.Account.Language, "EpicMount.Msg.CantWaterDrop"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    Owner.DismountSteed(true);
                }
                m_zTimer.Stop();
                m_zTimer = null;
                return 0;
            }

            if (newZ < 0 && m_zStep < 0 && waterLevel == 0)
            {
                newZ = 0;
                reached = true;
            }

            this.Position = Position.Create(this.CurrentRegionID, this.Position.X, this.Position.Y, newZ, this.Position.Orientation.InHeading);

            if (Owner != null && Owner.Steed == this)
            {
                Owner.Position = Position.Create(Owner.CurrentRegionID, Owner.Position.X, Owner.Position.Y, newZ, Owner.Position.Orientation.InHeading);
            }

            foreach (GamePlayer p in this.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).OfType<GamePlayer>())
            {
                if (p != null) p.Out.SendObjectUpdate(this);
            }

            if (reached)
            {
                m_zTimer.Stop();
                m_zTimer = null;
                return 0;
            }
            return 50;
        }

        public override void SaveIntoDatabase() { }
    }
}