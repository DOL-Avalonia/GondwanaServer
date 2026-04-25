using DOL.AI.Brain;
using DOL.Database;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using System.Collections.Generic;

namespace DOL.GS.Scripts
{
    public class GameCatapultSeat : GameNPC
    {
        public GameNPCCatapult ParentCatapult;

        public GameCatapultSeat() : base()
        {
            Model = 667;
            Name = "Catapult Seat";
            Level = 50;
            MaxSpeedBase = 0;
            Flags = eFlags.PEACE | eFlags.CANTTARGET | eFlags.FLYING;
            SetOwnBrain(new BlankBrain());
        }

        public override bool RiderDismount(bool forced, GamePlayer player)
        {
            bool success = base.RiderDismount(forced, player);
            if (success && ParentCatapult != null)
            {
                ParentCatapult.NotifyPlayerDismounted(player);
            }
            return success;
        }

        public override bool Interact(GamePlayer player)
        {
            if (ParentCatapult != null)
                return ParentCatapult.Interact(player);
            return false;
        }

        public override void Delete()
        {
            base.Delete();
            if (ParentCatapult != null && ParentCatapult.ObjectState != eObjectState.Deleted)
            {
                ParentCatapult.Delete();
            }
        }
    }


    public class GameNPCCatapult : GameSiegeWeapon
    {
        private GameStaticCatapult m_backbone;
        private GameCatapultSeat m_seat;
        private bool m_isFiring = false;

        public GameNPCCatapult(GameStaticCatapult backbone) : base()
        {
            m_backbone = backbone;
            Model = (ushort)(backbone.SecondaryModel > 0 ? backbone.SecondaryModel : 2598);
            Name = "Catapult";
            Level = 50;
            MaxSpeedBase = 0;
            Flags = eFlags.PEACE | eFlags.CANTTARGET;

            if (ActionDelay != null && ActionDelay.Length > 2)
            {
                int loadTime = (m_backbone != null && m_backbone.CoffreOpeningInterval > 0) ? m_backbone.CoffreOpeningInterval * 3000 : 5000;
                ActionDelay[(int)SiegeTimer.eAction.Arming] = loadTime;
            }
        }

        public GameNPCCatapult() : base()
        {
        }

        // Spawn the invisible seat at the offset when the catapult spawns
        public override bool AddToWorld()
        {
            bool success = base.AddToWorld();
            if (success && m_seat == null)
            {
                Vector offset = Vector.Create(this.Orientation, length: -100, z: 55);
                Position seatPos = this.Position + offset;

                m_seat = new GameCatapultSeat
                {
                    ParentCatapult = this,
                    Position = seatPos,
                    CurrentRegion = this.CurrentRegion,
                    Realm = this.Realm
                };
                m_seat.AddToWorld();
            }
            return success;
        }

        public override bool RemoveFromWorld()
        {
            if (m_seat != null)
            {
                m_seat.RemoveFromWorld();
                m_seat = null;
            }
            return base.RemoveFromWorld();
        }

        public override void Delete()
        {
            base.Delete();

            if (m_seat != null && m_seat.ObjectState != eObjectState.Deleted)
            {
                m_seat.Delete();
                m_seat = null;
            }

            if (m_backbone != null && m_backbone.ObjectState != eObjectState.Deleted)
            {
                m_backbone.DeleteFromDatabase();
                m_backbone.Delete();
            }
        }

        public override bool Interact(GamePlayer player)
        {
            if (!player.IsAlive) return false;

            string lang = player.Client?.Account?.Language ?? "EN";

            if (Owner != null && Owner != player)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "GameFunCatapult.AlreadyOperating"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (m_seat == null) return false;

            bool seatOccupied = false;
            if (m_seat.Riders != null)
            {
                foreach (GamePlayer rider in m_seat.Riders)
                {
                    if (rider != null)
                    {
                        seatOccupied = true;
                        break;
                    }
                }
            }

            if (seatOccupied && Owner != player)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "GameFunCatapult.SeatOccupied"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Consume the key when the player interacts to take control for the first time
            if (Owner == null && m_backbone != null && !string.IsNullOrEmpty(m_backbone.KeyItem))
            {
                InventoryItem key = player.Inventory.GetFirstItemByID(m_backbone.KeyItem, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                if (key == null)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "GameFunCatapult.MissingItem"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                if (m_backbone.KeyLoseDur > 0)
                {
                    key.Durability -= m_backbone.KeyLoseDur;
                    if (key.Durability <= 0)
                    {
                        player.Inventory.RemoveItem(key);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "GameFunCatapult.KeyBreaks"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        player.Out.SendInventoryItemsUpdate(new InventoryItem[] { key });
                    }
                }
                else
                {
                    player.Inventory.RemoveCountFromStack(key, 1);
                }
            }

            if (Owner != player)
            {
                player.MountSteed(m_seat, true);
            }

            return base.Interact(player);
        }

        // Called by the GameCatapultSeat if the player jumps off (moves or types /dismount)
        public void NotifyPlayerDismounted(GamePlayer player)
        {
            if (Owner == player && !m_isFiring)
            {
                string lang = player.Client?.Account?.Language ?? "EN";

                if (SiegeWeaponTimer != null && SiegeWeaponTimer.IsAlive)
                {
                    SiegeWeaponTimer.Stop();
                }

                CurrentState &= ~eState.Armed;
                ReleaseControl();

                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "GameFunCatapult.CancelLaunch"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        public override void Aim()
        {
            if (Owner is GamePlayer player)
            {
                string lang = player.Client?.Account?.Language ?? "EN";
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "GameFunCatapult.FixedAim"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        public override void Arm()
        {
            CurrentState |= eState.Aimed;
            base.Arm();
        }

        public override void Fire()
        {
            if (Owner == null) return;

            GamePlayer player = Owner as GamePlayer;
            if (player == null) return;
            string lang = player.Client?.Account?.Language ?? "EN";

            if ((CurrentState & eState.Armed) == 0)
            {
                if (Owner is GamePlayer p)
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "GameFunCatapult.NotArmed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            m_isFiring = true;
            CurrentState &= ~eState.Armed;

            this.TargetObject = player;
            this.GroundTargetPosition = player.Position;

            foreach (GamePlayer p in this.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                p.Out.SendSiegeWeaponFireAnimation(this, 0);
                p.Out.SendCombatAnimation(this, player, 0x0000, 0x0000, 0x00, 0x00, 0x14, player.HealthPercent);
            }

            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "GameFunCatapult.Launched"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            player.Heading = this.Heading;

            ReleaseControl();

            RegionTimer bumpTimer = new RegionTimer(this, new RegionTimerCallback(DelayedBumpCallback));
            bumpTimer.Properties.setProperty("target", player);
            bumpTimer.Start(100);
        }

        protected virtual int DelayedBumpCallback(RegionTimer timer)
        {
            GamePlayer player = timer.Properties.getProperty<GamePlayer>("target", null);
            if (player != null && player.IsAlive)
            {
                string lang = player.Client?.Account?.Language ?? "EN";
                player.DismountSteed(true);

                List<FollowingFriendMob> followersToBump = new List<FollowingFriendMob>();
                foreach (GameNPC npc in this.GetNPCsInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (npc is FollowingFriendMob follower && follower.PlayerFollow == player)
                    {
                        followersToBump.Add(follower);
                    }
                }

                if (m_backbone != null && m_backbone.PunishSpellId > 0)
                {
                    Spell bumpSpell = SkillBase.GetSpellByID(m_backbone.PunishSpellId);
                    if (bumpSpell != null)
                    {
                        SpellLine line = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);

                        ISpellHandler handler = ScriptMgr.CreateSpellHandler(this, bumpSpell, line);
                        if (handler is SpellHandler sh)
                        {
                            sh.ApplyEffectOnTarget(player, 1.0);

                            foreach (FollowingFriendMob follower in followersToBump)
                            {
                                follower.MoveWithoutRemovingFromWorld(this.Position, true);
                                follower.Heading = this.Heading;
                                sh.ApplyEffectOnTarget(follower, 1.0);
                            }
                        }
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "GameFunCatapult.Malfunction"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                }
            }

            m_isFiring = false;
            return 0;
        }
    }
}