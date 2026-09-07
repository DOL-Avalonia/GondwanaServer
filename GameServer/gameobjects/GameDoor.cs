using System;
using System.Linq;
using System.Collections.Generic;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.MobGroups;

namespace DOL.GS
{
    public class GameDoor : GameDoorBase
    {
        /// <summary>
        /// The time interval after which door will be closed, in milliseconds
        /// On live this is usually 5 seconds
        /// </summary>
        protected const int CLOSE_DOOR_TIME = 8000;

        private const int REPAIR_INTERVAL = 1000; // Heals every 1 second when broken
        private bool m_openDead = false;
        private int originalPunishSpellValue;

        protected ECSGameTimer m_closeDoorTimer;
        protected ECSGameTimer m_doorHealthRegenTimer;

        public override bool CanBeOpenedViaInteraction => Locked == 0;

        public GameDoor() : base()
        {
            m_model = 0xFFFF;
        }

        #region Custom Server Properties

        private string m_group_mob_id;
        private string m_switchFamily;
        private string m_key;
        private short m_key_Chance;
        private bool m_isRenaissance;
        private int m_punishSpell;

        public string Group_Mob_Id { get => m_group_mob_id; set => m_group_mob_id = value; }
        public string Key { get => m_key; set => m_key = value; }
        public short Key_Chance { get => m_key_Chance; set => m_key_Chance = value; }
        public bool IsRenaissance { get => m_isRenaissance; set => m_isRenaissance = value; }
        public int PunishSpell { get => m_punishSpell; set => m_punishSpell = value; }
        public string SwitchFamily { get => m_switchFamily; set => m_switchFamily = value; }

        public override int MaxHealth => 5 * GetModified(eProperty.MaxHealth);
        public virtual byte Status => 0x00;

        #endregion

        /// <summary>
        /// Loads the custom properties of this overworld door from a door table slot
        /// </summary>
        /// <param name="obj">DBDoor</param>
        public override void LoadFromDatabase(DataObject obj)
        {
            base.LoadFromDatabase(obj);

            if (obj is not DBDoor dbDoor) return;

            m_group_mob_id = dbDoor.Group_Mob_Id;
            m_key = dbDoor.Key;
            m_key_Chance = dbDoor.Key_Chance;
            m_isRenaissance = dbDoor.IsRenaissance;
            m_punishSpell = dbDoor.PunishSpell;
            m_switchFamily = dbDoor.SwitchFamily;
            originalPunishSpellValue = dbDoor.PunishSpell;

            // Open mile gates on PVE and PVP server types
            if (CurrentRegion != null && CurrentRegion.IsFrontier &&
                (GameServer.Instance.Configuration.ServerType == eGameServerType.GST_PvE || GameServer.Instance.Configuration.ServerType == eGameServerType.GST_PvP))
            {
                State = eDoorState.Open;
            }
        }

        /// <summary>
        /// save the custom properties of this overworld door to a door table slot
        /// </summary>
        public override void SaveIntoDatabase()
        {
            if (DbDoor != null)
            {
                DbDoor.Group_Mob_Id = m_group_mob_id;
                DbDoor.Key = m_key;
                DbDoor.Key_Chance = m_key_Chance;
                DbDoor.IsRenaissance = m_isRenaissance;
                DbDoor.PunishSpell = m_punishSpell;
                DbDoor.SwitchFamily = m_switchFamily;
            }
            base.SaveIntoDatabase();
        }

        private void TriggerPunishSpell(GameLiving opener)
        {
            if (PunishSpell > 0 && opener != null)
            {
                DBSpell punishspell = GameServer.Database.SelectObjects<DBSpell>(DB.Column("SpellID").IsEqualTo(PunishSpell)).FirstOrDefault();

                if (punishspell != null)
                {
                    foreach (GamePlayer pl in opener.GetPlayersInRadius(5000))
                    {
                        pl.Out.SendSpellEffectAnimation(opener, opener, (ushort)PunishSpell, 0, false, 5);
                    }
                    if (opener is GamePlayer player)
                        player.Out.SendSpellEffectAnimation(opener, opener, (ushort)PunishSpell, 0, false, 5);

                    opener.TakeDamage(opener, eDamageType.Energy, (int)punishspell.Damage, 0);
                }
            }
        }

        #region Switches
        public void UnlockBySwitch()
        {
            if (Locked == 1)
            {
                Locked = 0;
                State = eDoorState.Open;
                PunishSpell = 0;
                SaveIntoDatabase();
            }
        }

        public void LockBySwitch()
        {
            if (Locked == 0)
            {
                Locked = 1;
                State = eDoorState.Closed;
                PunishSpell = originalPunishSpellValue;
                SaveIntoDatabase();
            }
        }

        public void OpenBySwitch()
        {
            UnlockBySwitch();
            if (HealthPercent > 40 || !m_openDead)
            {
                lock (m_stateLock)
                {
                    if (m_closeDoorTimer == null || !m_closeDoorTimer.IsAlive)
                        m_closeDoorTimer = new ECSGameTimer(this, CloseDoorTimerCallback, CLOSE_DOOR_TIME);
                }
            }
        }
        #endregion

        /// <summary>
        /// Call this function to open the door
        /// </summary>
        public override void Open(GameLiving opener = null)
        {
            GamePlayer player = opener as GamePlayer;

            if (!String.IsNullOrEmpty(Group_Mob_Id) && MobGroupManager.Instance.Groups.ContainsKey(Group_Mob_Id))
            {
                bool allDead = MobGroupManager.Instance.Groups[Group_Mob_Id].NPCs.All(m => !m.IsAlive);
                if (!allDead)
                {
                    if (player != null) player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.GameDoor.NeedKillGroupMob"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    TriggerPunishSpell(opener);
                    return;
                }
            }

            if (IsRenaissance && player != null && !player.IsRenaissance)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.GameDoor.NeedReborn"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                TriggerPunishSpell(opener);
                return;
            }

            if (!String.IsNullOrEmpty(Key) && opener != null)
            {
                var keyItem = opener.Inventory.GetFirstItemByID(Key, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                if (keyItem == null)
                {
                    if (player != null) player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.GameDoor.NeedKey"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    TriggerPunishSpell(opener);
                    return;
                }
                if (Key.StartsWith("oneuse"))
                {
                    opener.Inventory.RemoveCountFromStack(keyItem, 1);
                }
            }

            if (Util.Chance(Key_Chance))
            {
                if (player != null) player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.GameDoor.FailOpen"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (Locked == 1 && !string.IsNullOrEmpty(SwitchFamily))
            {
                if (player != null) player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.GameDoor.NeedSwitch"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                TriggerPunishSpell(opener);
                return;
            }

            if (Locked == 0)
                State = eDoorState.Open;

            if (HealthPercent > 40 || !m_openDead)
            {
                lock (m_stateLock)
                {
                    if (m_closeDoorTimer == null || !m_closeDoorTimer.IsAlive)
                        m_closeDoorTimer = new ECSGameTimer(this, CloseDoorTimerCallback, CLOSE_DOOR_TIME);
                }
            }
        }

        public override void Close(GameLiving closer = null)
        {
            if (!m_openDead)
                State = eDoorState.Closed;

            if (m_closeDoorTimer != null && m_closeDoorTimer.IsAlive)
            {
                m_closeDoorTimer.Stop();
                m_closeDoorTimer = null;
            }
        }

        protected virtual int CloseDoorTimerCallback(ECSGameTimer timer)
        {
            Close();
            return 0;
        }

        public override int Health
        {
            get => m_health;
            set
            {
                int maxHealth = MaxHealth;
                if (value >= maxHealth)
                {
                    m_health = maxHealth;
                    XPGainers.Clear();
                }
                else
                {
                    m_health = value > 0 ? value : 0;
                }

                if (IsAlive && m_health < maxHealth)
                    StartHealthRegeneration();
            }
        }

        public override void Die(GameObject killer)
        {
            base.Die(killer);
            StartHealthRegeneration();
        }

        public override void StartHealthRegeneration()
        {
            if (m_doorHealthRegenTimer != null && m_doorHealthRegenTimer.IsAlive) return;
            if (Health >= MaxHealth) return;

            m_doorHealthRegenTimer = new ECSGameTimer(this, HealthRegenerationTimerCallback, REPAIR_INTERVAL);
        }

        // RegionTimer properly handled safely now instead of static memory leak!
        protected virtual int HealthRegenerationTimerCallback(ECSGameTimer timer)
        {
            if (HealthPercent >= 100)
            {
                timer.Stop();
                return 0;
            }

            if (!InCombat)
            {
                Health += Math.Max(1, Level * 2);

                if (HealthPercent >= 40 && m_openDead)
                {
                    m_openDead = false;
                    Close();
                }

                if (Health >= MaxHealth)
                {
                    m_openDead = false;
                    Close();
                    timer.Stop();
                    return 0;
                }
            }

            return REPAIR_INTERVAL;
        }

        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            if (!m_openDead && Realm != eRealm.Door)
                base.TakeDamage(source, damageType, damageAmount, criticalAmount);

            if (source is not GamePlayer attackerPlayer || m_openDead || Realm == eRealm.Door)
                return;

            attackerPlayer.Out.SendMessage(LanguageMgr.GetTranslation(attackerPlayer.Client.Account.Language, "DoorRequestHandler.GameDoor.NowOpen", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            Health -= damageAmount + criticalAmount;

            if (IsAlive)
                return;

            attackerPlayer.Out.SendMessage(LanguageMgr.GetTranslation(attackerPlayer.Client.Account.Language, "DoorRequestHandler.GameDoor.NowOpen", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            Die(source);
            m_openDead = true;

            if (Locked == 0)
                Open();

            Group attackerGroup = attackerPlayer.Group;
            if (attackerGroup != null)
            {
                foreach (GameLiving living in attackerGroup.GetMembersInTheGroup())
                    (living as GamePlayer)?.Out.SendMessage(LanguageMgr.GetTranslation(attackerPlayer.Client.Account.Language, "DoorRequestHandler.GameDoor.NowOpen", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
    }
}