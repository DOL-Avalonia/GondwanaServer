using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Numerics;
using DOL.Database;
using DOL.GameEvents;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.Database.Attributes;
using DOL.Territories;
using log4net;
using DOL.MobGroups;
using DOL.Events;
using DOL.GS.GameEvents;
using DOL.GS.Geometry;

namespace DOL.GS.Scripts
{
    public class GameCoffre : GameStaticItem
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        public const string CROCHET = "Crochet"; //Id_nb des crochets
        public const int UNLOCK_TIME = 10; //Temps pour crocheter une serrure en secondes
        public readonly int LARGE_ITEM_DIST = 500;
        private GamePlayer m_interactPlayer;
        private DateTime m_lastInteract;
        public int TPID { get; set; }
        public bool ShouldRespawnToTPID { get; set; }
        public int CurrentStep { get; set; }
        public bool PickOnTouch { get; set; }
        public bool IsOpenableOnce { get; set; }
        public bool IsTerritoryLinked { get; set; }
        public int KeyLoseDur { get; set; }
        public string SwitchFamily { get; set; }
        public int SwitchOrder { get; set; }
        public bool IsSwitch { get; set; }
        public bool WrongOrderResetFamily { get; set; }
        public int SecondaryModel { get; set; }
        public string ActivatedBySwitchOn { get; set; }
        public string ActivatedBySwitchOff { get; set; }
        public string ResetBySwitchOn { get; set; }
        public string ResetBySwitchOff { get; set; }
        public int SwitchOnSound { get; set; }
        public int WrongFamilyOrderSound { get; set; }
        public int ActivatedFamilySound { get; set; }
        public int DeactivatedFamilySound { get; set; }

        public bool isActivated;
        private Timer proximityTimer;
        private Timer activationTimer;
        public int ActivatedDuration { get; set; }

        private void ShowSecondaryModel()
        {
            if (SecondaryModel > 0 && IsSwitch)
            {
                Model = (ushort)SecondaryModel;
            }
        }

        private void RevertToPrimaryModel()
        {
            if (SecondaryModel > 0)
            {
                Model = Coffre.Model;
            }
        }

        private bool HasPlayerOpened(GamePlayer player)
        {
            var openedCoffre = GameServer.Database.SelectObject<CoffrexPlayer>(DB.Column("PlayerID").IsEqualTo(player.InternalID).And(DB.Column("CoffreID").IsEqualTo(this.InternalID)));
            return openedCoffre != null;
        }

        private void MarkCoffreAsOpenedByPlayer(GamePlayer player)
        {
            var newEntry = new CoffrexPlayer { PlayerID = player.InternalID, CoffreID = this.InternalID };
            GameServer.Database.AddObject(newEntry);
        }

        #region Variables - Constructeur
        private DBCoffre Coffre;
        private IList<CoffreItem> m_Items;
        public IList<CoffreItem> Items
        {
            get { return m_Items; }
        }
        
        public static readonly Dictionary<string, List<GameCoffre>> ChestsByFamily = new();
        public static readonly Dictionary<string, List<GameCoffre>> ChestsByName = new(); // Do we really need this? That's a lot of maintenance for one command... (/chest respawn)

        private int m_ItemChance;
        /// <summary>
        /// Pourcentage de chance de trouver un item dans le coffre (si 0 alors 50% de chance)
        /// </summary>
        public int ItemChance
        {
            get { return m_ItemChance; }
            set
            {
                if (value > 100)
                    m_ItemChance = 100;
                else if (value < 0)
                    m_ItemChance = 0;
                else
                    m_ItemChance = value;
            }
        }

        public override bool IsCoffre
        {
            get
            {
                return true;
            }
        }

        public int TrapRate
        {
            get;
            set;
        }

        public string NpctemplateId
        {
            get;
            set;
        }

        public int TpX
        {
            get;
            set;
        }

        public int TpY
        {
            get;
            set;
        }

        public int TpZ
        {
            get;
            set;
        }

        public int TPHeading
        {
            get;
            set;
        }

        public bool IsTeleporter
        {
            get;
            set;
        }

        public bool HasPickableAnim
        {
            get;
            set;
        }

        public int TpLevelRequirement
        {
            get;
            set;
        }

        public bool TpIsRenaissance
        {
            get;
            set;
        }

        public bool IsLargeCoffre
        {
            get;
            set;
        }
        public string RemovedByEventID
        {
            get;
            set;
        }

        public int TpEffect
        {
            get;
            set;
        }

        public int TpRegion
        {
            get;
            set;
        }

        public bool IsOpeningRenaissanceType
        {
            get;
            set;
        }

        public int PunishSpellId
        {
            get;
            set;
        }


        public int CoffreOpeningInterval
        {
            get;
            set;
        }

        public DateTime? LastTimeChecked
        {
            get;
            set;
        }

        public int AllChance
        {
            get
            {
                return m_Items.Sum(item => item.Chance);
            }
        }

        public List<ILootGenerator>? LootGenerators
        {
            get;
            set;
        }

        public DateTime LastOpen;
        /// <summary>
        /// Temps de réapparition d'un item (en minutes)
        /// </summary>
        public string KeyItem = "";
        public int LockDifficult;

        public GameCoffre()
        {
            LastOpen = DateTime.MinValue;
            RespawnInterval = 5 * 60;
            m_Items = new List<CoffreItem>();
        }

        public GameCoffre(IList<CoffreItem> Items)
        {
            HasPickableAnim = true;
            LastOpen = DateTime.MinValue;
            HasPickableAnim = true;
            RespawnInterval = 5 * 60;
            m_Items = Items;
        }
        #endregion


        private class ChestComparer : IComparer<GameCoffre>
        {
            public int Compare(GameCoffre x, GameCoffre y)
            {
                if (ReferenceEquals(x, y))
                    return 0;
                if (y is null)
                    return 1;
                if (x is null)
                    return -1;
                return x.SwitchOrder.CompareTo(y.SwitchOrder);
            }
        }

        public override bool AddToWorld()
        {
            base.AddToWorld();

            if (!string.IsNullOrEmpty(Name))
            {
                lock (ChestsByName)
                {
                    if (!ChestsByName.TryGetValue(Name, out List<GameCoffre> list))
                    {
                        list = new List<GameCoffre>(){ this };
                        ChestsByName[Name] = list;
                    }
                    else
                    {
                        list.Add(this);
                    }
                }
            }

            if (!string.IsNullOrEmpty(SwitchFamily))
            {
                lock (ChestsByFamily)
                {
                    if (!ChestsByFamily.TryGetValue(SwitchFamily, out List<GameCoffre> list))
                    {
                        list = new List<GameCoffre>(){ this };
                        ChestsByFamily[SwitchFamily] = list;
                    }
                    else
                    {
                        int where = int.Abs(list.BinarySearch(this, new ChestComparer()));
                        if (where <= list.Count)
                            list.Insert(where, this);
                        else
                            list.Add(this);
                    }
                }
            }

            if (PickOnTouch)
            {
                proximityTimer = new Timer(1500);
                proximityTimer.Elapsed += (sender, e) => CheckPlayerProximity();
                proximityTimer.Start();
            }

            return true;
        }

        public override bool RemoveFromWorld()
        {
            if (proximityTimer != null)
            {
                proximityTimer.Stop();
                proximityTimer.Dispose();
                proximityTimer = null;
            }

            if (!string.IsNullOrEmpty(SwitchFamily))
            {
                lock (ChestsByFamily)
                {
                    if (ChestsByFamily.TryGetValue(SwitchFamily, out List<GameCoffre> list))
                    {
                        list.Remove(this);
                    }
                }
            }
            if (!string.IsNullOrEmpty(Name))
            {
                lock (ChestsByName)
                {
                    if (ChestsByName.TryGetValue(Name, out List<GameCoffre> list))
                    {
                        list.Remove(this);
                    }
                }
            }
            return base.RemoveFromWorld();
        }

        private void CheckPlayerProximity()
        {
           if (PickOnTouch)
            {
                foreach (GamePlayer player in GetPlayersInRadius(90))
                {
                    if (player.IsAlive && IsWithinRadius(player, 90) && !HasPlayerOpened(player) && (this.CoffreOpeningInterval == 0 || !this.LastTimeChecked.HasValue || (DateTime.Now - this.LastTimeChecked.Value) > TimeSpan.FromMinutes(this.CoffreOpeningInterval)))
                    {
                        if (IsSwitch)
                        {
                            if (IsTerritoryLinked)
                            {
                                var territory = TerritoryManager.GetCurrentTerritory(this);
                                bool allowed = false;
                                if (territory != null && !territory.IsNeutral())
                                {
                                    if (player.Guild != null && player.Guild == territory.OwnerGuild)
                                        allowed = true;
                                }
                                if (!allowed) continue;
                            }

                            if (!isActivated && !HandleLockedSwitch(player))
                            {
                                continue;
                            }
                            CheckSwitchActivation(player);
                        }
                        else
                        {
                            InteractEnd(player);
                        }
                    }
                }
            }
        }

        private bool HandleLockedSwitch(GamePlayer player)
        {
            if (!string.IsNullOrEmpty(KeyItem))
            {
                InventoryItem key = player.Inventory.GetFirstItemByID(KeyItem, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                if (key != null)
                {
                    if (Util.Chance(ItemChance))
                    {
                        // Successfully unlocked
                        if (KeyLoseDur > 0)
                        {
                            key.Durability -= KeyLoseDur;
                            if (key.Durability <= 0)
                            {
                                player.Inventory.RemoveItem(key);
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.KeyDestroyed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                player.Out.SendInventoryItemsUpdate(new InventoryItem[] { key });
                            }
                        }
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.SwitchUnlocked"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        return true;
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.SwitchUnlockFailed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        if (KeyLoseDur > 0)
                        {
                            key.Durability -= KeyLoseDur / 2;
                            if (key.Durability <= 0)
                            {
                                player.Inventory.RemoveItem(key);
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.KeyDestroyed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                player.Out.SendInventoryItemsUpdate(new InventoryItem[] { key });
                            }
                        }
                        return false;
                    }
                }
                else
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.KeyRequired"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }
            return true;
        }

        private void CheckSwitchActivation(GamePlayer player)
        {
            if (!IsSwitch || isActivated) return;

            List<GameCoffre> switchesInFamily;

            if (string.IsNullOrEmpty(SwitchFamily))
            {
                switchesInFamily = new List<GameCoffre>(){ this };
            }
            else
            {
                lock (ChestsByFamily)
                {
                    if (ChestsByFamily.TryGetValue(SwitchFamily, out List<GameCoffre> list))
                    {
                        switchesInFamily = list;
                    }
                    else
                    {
                        switchesInFamily = new List<GameCoffre>() { this };
                    }
                }
            }

            int currentIndex = switchesInFamily.IndexOf(this);
            if (currentIndex == -1 || (currentIndex > 0 && !switchesInFamily[currentIndex - 1].isActivated))
            {
                if (WrongOrderResetFamily)
                {
                    // Reset the entire family order
                    foreach (var switchCoffre in switchesInFamily)
                    {
                        switchCoffre.isActivated = false;
                        switchCoffre.RevertToPrimaryModel();
                    }
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.SwitchOrderReset"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    player.Out.SendSoundEffect((ushort)WrongFamilyOrderSound, Position, 0);
                }
                else
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.SwitchCannotActivate"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                return;
            }

            isActivated = true;
            ShowSecondaryModel();
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.SwitchActivated"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            player.Out.SendSoundEffect((ushort)SwitchOnSound, Position, 0);

            if (switchesInFamily.All(c => c.isActivated))
            {
                ActivateSwitchFamily(player);
            }

            player.Notify(GameObjectEvent.SwitchActivated, this, new SwitchEventArgs(this, player));
        }

        private void ActivateSwitchFamily(GamePlayer player)
        {
            OpenLinkedDoors();
            KillLinkedMobs();

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.SwitchAllActivated"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            player.Out.SendSoundEffect((ushort)ActivatedFamilySound, Position, 0);

            if (!string.IsNullOrEmpty(ActivatedBySwitchOn))
            {
                var gameEvent = GameEventManager.Instance.GetEventByID(ActivatedBySwitchOn);
                if (gameEvent != null)
                {
                    GameEventManager.Instance.StartEvent(gameEvent);
                }
            }

            if (!string.IsNullOrEmpty(ResetBySwitchOn))
            {
                var resetEvent = GameEventManager.Instance.GetEventByID(ResetBySwitchOn);
                if (resetEvent != null)
                {
                    GameEventManager.Instance.StopEvent(resetEvent, EndingConditionType.Switch);
                }
            }

            if (ActivatedDuration > 0)
            {
                activationTimer = new Timer(ActivatedDuration * 1000);
                activationTimer.Elapsed += (sender, e) => DeactivateSwitchFamily();
                activationTimer.Start();
            }
        }

        private void OpenLinkedDoors()
        {
            var doorsToOpen = DoorMgr.GetDoorsBySwitchFamily(SwitchFamily);
            foreach (var door in doorsToOpen)
            {
                if (door is GameDoor gameDoor)
                {
                    gameDoor.UnlockBySwitch();
                }
            }
        }

        private void Deactivate()
        {
            isActivated = false;
            RevertToPrimaryModel();
        }

        private void DeactivateSwitchFamily()
        {
            activationTimer.Stop();

            lock (ChestsByFamily)
            {
                if (ChestsByFamily.TryGetValue(SwitchFamily, out List<GameCoffre> list))
                {
                    list.ForEach(c => c.Deactivate());
                }
                else
                {
                    Deactivate();
                }
            }

            RevertDoors();

            var mobsToRespawn = MobGroupManager.Instance.GetMobsBySwitchFamily(SwitchFamily);
            foreach (var mob in mobsToRespawn)
            {
                mob.AddToWorld();
            }

            foreach (var obj in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (obj is GamePlayer player)
                {
                    player.Out.SendSoundEffect((ushort)DeactivatedFamilySound, Position, 0);
                }
            }

            if (!string.IsNullOrEmpty(ActivatedBySwitchOff))
            {
                var gameEvent = GameEventManager.Instance.GetEventByID(ActivatedBySwitchOff);
                if (gameEvent != null)
                {
                    GameEventManager.Instance.StartEvent(gameEvent);
                }
            }

            if (!string.IsNullOrEmpty(ResetBySwitchOff))
            {
                var resetEvent = GameEventManager.Instance.GetEventByID(ResetBySwitchOff);
                if (resetEvent != null)
                {
                    GameEventManager.Instance.StopEvent(resetEvent, EndingConditionType.Switch);
                }
            }
        }

        private void RevertDoors()
        {
            var doorsToRevert = DoorMgr.GetDoorsBySwitchFamily(SwitchFamily);
            foreach (var door in doorsToRevert)
            {
                if (door is GameDoor gameDoor)
                {
                    gameDoor.LockBySwitch();
                }
            }
        }

        private void KillLinkedMobs()
        {
            var mobsToKill = MobGroupManager.Instance.GetMobsBySwitchFamily(SwitchFamily);
            foreach (var mob in mobsToKill)
            {
                mob.Die(null);
            }
        }

        /// <summary>
        ///  Get Coffres and their new EventIds from Db
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Tuple<GameStaticItem, string>> GetCoffresUsedInEventsInDb(ushort region)
        {
            var coffres = GameServer.Database.SelectObjects<DBCoffre>(DB.Column("EventID").IsNotNull().And(DB.Column("Region").IsEqualTo(region)));

            if (coffres == null)
            {
                return null;
            }

            List<Tuple<GameStaticItem, string>> values = new List<Tuple<GameStaticItem, string>>();

            foreach (var dbCoffre in coffres)
            {
                GameCoffre coffreInRegion = WorldMgr.Regions[region].Objects.FirstOrDefault(o => o != null && o as GameCoffre != null && o.InternalID.Equals(dbCoffre.ObjectId)) as GameCoffre;

                if (coffreInRegion != null)
                {
                    values.Add(new Tuple<GameStaticItem, string>(coffreInRegion, dbCoffre.EventID));
                }
            }

            return values;
        }

        private TPPoint GetSmartNextTPPoint(IList<DBTPPoint> tpPoints)
        {
            TPPoint smartNextPoint = null;
            int maxPlayerCount = 0;

            foreach (var tpPoint in tpPoints)
            {
                int playerCount = WorldMgr.GetPlayersCloseToSpot(Position.Create(tpPoint.Region, tpPoint.X, tpPoint.Y, tpPoint.Z), 1500).OfType<GamePlayer>().Count(); // Using 1500 directly
                if (playerCount > maxPlayerCount)
                {
                    maxPlayerCount = playerCount;
                    smartNextPoint = new TPPoint(tpPoint.Region, tpPoint.X, tpPoint.Y, tpPoint.Z, eTPPointType.Smart, tpPoint);
                }
            }

            return smartNextPoint ?? new TPPoint(tpPoints.First().Region, tpPoints.First().X, tpPoints.First().Y, tpPoints.First().Z, eTPPointType.Smart, tpPoints.First());
        }

        private TPPoint GetLoopNextTPPoint(IList<DBTPPoint> tpPoints)
        {
            DBTPPoint currentDBTPPoint = tpPoints.FirstOrDefault(p => p.Step == CurrentStep) ?? tpPoints.First();
            TPPoint tpPoint = new TPPoint(currentDBTPPoint.Region, currentDBTPPoint.X, currentDBTPPoint.Y, currentDBTPPoint.Z, eTPPointType.Loop, currentDBTPPoint);
            CurrentStep = (CurrentStep % tpPoints.Count) + 1;
            return tpPoint;
        }

        private TPPoint GetRandomTPPoint(IList<DBTPPoint> tpPoints)
        {
            DBTPPoint randomDBTPPoint = tpPoints[Util.Random(tpPoints.Count - 1)];
            return new TPPoint(randomDBTPPoint.Region, randomDBTPPoint.X, randomDBTPPoint.Y, randomDBTPPoint.Z, eTPPointType.Random, randomDBTPPoint);
        }

        public void RespawnCoffre()
        {
            base.AddToWorld();
        }

        #region Interact - GetRandomItem
        public override bool Interact(GamePlayer player)
        {
            if (PickOnTouch && !HasPlayerOpened(player))
            {
                return InteractEnd(player);
            }

            if (IsSwitch)
            {
                if (!isActivated && !HandleLockedSwitch(player))
                {
                    return false;
                }
                CheckSwitchActivation(player);
                return true;
            }

            if (IsOpenableOnce && HasPlayerOpened(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.UniqueTreasureTaken"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (IsTerritoryLinked)
            {
                var territory = TerritoryManager.GetCurrentTerritory(this);
                bool accessGranted = false;

                if (territory != null && !territory.IsNeutral())
                {
                    if (player.Guild != null && player.Guild == territory.OwnerGuild)
                    {
                        accessGranted = true;
                    }
                }

                if (!accessGranted)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.TerritoryNotOwned"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            if (!base.Interact(player) || !player.IsAlive) return false;
            
            if (!this.IsWithinRadius(player, (IsLargeCoffre ? LARGE_ITEM_DIST : WorldMgr.GIVE_ITEM_DISTANCE + 60))) return false;

            if (IsOpenableOnce)
            {
                MarkCoffreAsOpenedByPlayer(player);
            }

            if (HasPickableAnim)
            {
                foreach (GamePlayer otherPlayer in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (otherPlayer != null)
                        otherPlayer.Out.SendEmoteAnimation(player, eEmote.PlayerPickup);
                }
            }

            if (LockDifficult > 0 || KeyItem != "")
            {
                if (m_interactPlayer != null && player != m_interactPlayer && (m_lastInteract.Ticks + 200000000) > DateTime.Now.Ticks)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.AlreadyOpen"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    return false;
                }
                InventoryItem it = player.Inventory.GetFirstItemByID(KeyItem, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                if (KeyItem != "" && it != null)
                {
                    if (!KeyItem.StartsWith("oneuse"))
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.Oneuse") + it.Name + ".", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        if (KeyLoseDur > 0)
                        {
                            it.Durability -= KeyLoseDur;
                            if (it.Durability <= 0)
                            {
                                player.Inventory.RemoveItem(it);
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.KeyDestroyed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                player.Out.SendInventoryItemsUpdate(new InventoryItem[] { it });
                            }
                        }
                    }
                    else
                    {
                        player.TempProperties.setProperty("CoffreItem", it);
                        player.Out.SendCustomDialog(LanguageMgr.GetTranslation(player.Client, "GameChest.UseItem", it.Name), OneUseOpen);
                        m_interactPlayer = player;
                        m_lastInteract = DateTime.Now;
                        return true;
                    }
                }
                else if (LockDifficult > 0 && player.Inventory.GetFirstItemByID(CROCHET, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack) != null)
                {
                    player.Out.SendCustomDialog(LanguageMgr.GetTranslation(player.Client, "GameChest.PickLock"), Unlock);
                    m_interactPlayer = player;
                    m_lastInteract = DateTime.Now;
                    return true;
                }
                else
                {
                    if (LockDifficult == 0 && KeyItem != "")
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.LockDifficult1"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    else if (LockDifficult > 0 && KeyItem == "")
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.LockDifficult2"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    else
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.LockDifficult3"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            return InteractEnd(player);
        }

        private void OneUseOpen(GamePlayer player, byte response)
        {
            if (response == 0x00) return;
            InventoryItem it = player.TempProperties.getProperty<InventoryItem>("CoffreItem", null);
            player.TempProperties.removeProperty("CoffreItem");
            if (it == null || m_interactPlayer != player || !player.Inventory.RemoveCountFromStack(it, 1))
            {
                if (m_interactPlayer == player)
                {
                    m_interactPlayer = null;
                    m_lastInteract = DateTime.MinValue;
                }
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.ItemError"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            InventoryLogging.LogInventoryAction(player, this, eInventoryActionType.Loot, it, 1);
            InteractEnd(player);
        }

        private bool TryOpen(GamePlayer player)
        {
            if (IsTeleporter)
            {
                bool failed = false;
                if (TpLevelRequirement > 0 && player.Level < TpLevelRequirement)
                {
                    failed = true;
                }

                if (TpIsRenaissance && !player.IsRenaissance)
                {
                    failed = true;
                }

                if (failed)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.Cannotteleport"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    return false;
                }
                HandleTeleporter(player);
                return true;
            }
            
            if (this.CoffreOpeningInterval != 0 && this.LastTimeChecked.HasValue && (DateTime.Now - this.LastTimeChecked.Value) <= TimeSpan.FromMinutes(this.CoffreOpeningInterval))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.AlreadyOpen2"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (IsOpeningRenaissanceType && !player.IsRenaissance)
            {
                if (PunishSpellId > 0)
                {
                    var spell = GameServer.Database.SelectObject<DBSpell>(DB.Column("SpellID").IsEqualTo(PunishSpellId));

                    if (spell != null)
                    {
                        foreach (GamePlayer pl in this.GetPlayersInRadius(5000))
                        {
                            pl.Out.SendSpellEffectAnimation(pl, pl, (ushort)PunishSpellId, 0, false, 5);
                        }
                        player.TakeDamage(this, eDamageType.Energy, (int)spell.Damage, 0);
                    }
                }
                HandlePopMob();
                return false;
            }
            this.LastTimeChecked = DateTime.Now;
            CoffreItem coffre = GetRandomItem(player);
            LootList loot = new LootList();
            bool error = false;
            if (!string.IsNullOrEmpty(coffre.Id_nb) && coffre.Chance != 0)
            {
                ItemTemplate item = GameServer.Database.SelectObject<ItemTemplate>(DB.Column("Id_nb").IsEqualTo(GameServer.Database.Escape(coffre.Id_nb)));
                if (item == null)
                {
                    log.Warn($"Item generated in chest {InternalID} for player {player.Name} ({player.InternalID}) not found in DB with id {coffre.Id_nb}");
                    error = true;
                }
                else
                {
                    loot.AddFixed(item, 1);
                }
            }

            foreach (var lootgenerator in LootGenerators ?? new List<ILootGenerator>())
            {
                loot.AddAll(lootgenerator.GenerateLoot(this, player));
            }

            var items = loot.GetLoot();
            if (items.Length > 0)
            {
                GetItems(player, items);
            }
            else
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, error ? "GameChest.NothingInteresting2" : "GameChest.NothingInteresting1"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            HandlePopMob();
            return true;
        }

        private bool InteractEnd(GamePlayer player)
        {
            if (TryOpen(player))
            {
                m_lastInteract = DateTime.MinValue;
                LastOpen = DateTime.Now;
                if (ShouldRespawnToTPID && TPID > 0 && !IsSwitch)
                {
                    IList<DBTPPoint> tpPoints = GameServer.Database.SelectObjects<DBTPPoint>(DB.Column("TPID").IsEqualTo(TPID));
                    DBTP dbtp = GameServer.Database.SelectObjects<DBTP>(DB.Column("TPID").IsEqualTo(TPID)).FirstOrDefault();

                    if (tpPoints != null && tpPoints.Count > 0 && dbtp != null)
                    {
                        TPPoint tpPoint = null;
                        switch ((eTPPointType)dbtp.TPType)
                        {
                            case eTPPointType.Loop:
                                tpPoint = GetLoopNextTPPoint(tpPoints);
                                break;

                            case eTPPointType.Random:
                                tpPoint = GetRandomTPPoint(tpPoints);
                                break;

                            case eTPPointType.Smart:
                                tpPoint = GetSmartNextTPPoint(tpPoints);
                                break;
                        }
                        if (tpPoint != null)
                        {
                            base.MoveTo(tpPoint.Region, (float)tpPoint.Position.X, (float)tpPoint.Position.Y, (float)tpPoint.Position.Z, Coffre.Heading);
                        }
                    }
                }

                if (RespawnInterval != 0)
                {
                    var respawnInterval = this.EventID != null && !CanRespawnWithinEvent ? 0 : RespawnInterval;
                
                    RemoveFromWorld(respawnInterval);
                }
            }

            ShowSecondaryModel();

            m_interactPlayer = null;
            SaveIntoDatabase();

            return true;
        }

        private bool GetItems(GamePlayer player, IEnumerable<ItemTemplate> items)
        {
            int count = 0;
            foreach (var item in items)
            {
                if (!player.Inventory.AddTemplate(GameInventoryItem.Create(item), 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.GetItemBagFull"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    break;
                }
                var name = !string.IsNullOrEmpty(item.TranslationId) ? LanguageMgr.GetTranslation(player.Client, item.TranslationId) : item.Name;
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.GetItem", name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                ++count;
            }
            return count > 0;
        }

        private void HandleTeleporter(GamePlayer player)
        {
            if (TpX > 0 && TpY > 0 && TpZ > 0 && TpRegion > 0)
            {
                RegionTimer TimerTL = new RegionTimer(this, Teleportation);
                TimerTL.Properties.setProperty("TP", new GameLocation("Coffre Location", (ushort)TpRegion, TpX, TpY, TpZ, (ushort)TPHeading));
                TimerTL.Properties.setProperty("player", player);
                TimerTL.Start(3000);

                {
                    if (TpEffect > 0)
                    {
                        //handle effect
                        player.Out.SendSpellCastAnimation(player, (ushort)TpEffect, 20);
                    }
                }
            }
        }

        private int Teleportation(RegionTimer timer)
        {
            GameLocation pos = timer.Properties.getProperty<GameLocation>("TP", null);
            GamePlayer player = timer.Properties.getProperty<GamePlayer>("player", null);

            player.MoveTo(pos.RegionID, pos.Position.X, pos.Position.Y, pos.Position.Z, pos.Heading);

            return 0;
        }

        private void HandlePopMob()
        {
            if (TrapRate > 0 && NpctemplateId != null)
            {
                var rand = new Random(DateTime.Now.Millisecond);
                if (rand.Next(1, TrapRate + 1) <= TrapRate)
                {
                    var template = GameServer.Database.SelectObject<DBNpcTemplate>(DB.Column("TemplateId").IsEqualTo(NpctemplateId));
                    if (template != null)
                    {
                        var mob = new AmteMob(new NpcTemplate(template))
                        {
                            Position = this.Position,
                            Size = 50,
                            Name = template.Name
                        };

                        mob.RespawnInterval = -1;
                        mob.AddToWorld();
                    }
                }
            }
        }

        private void Repop_Elapsed(object sender, ElapsedEventArgs e)
        {
            AddToWorld();
        }
        #endregion

        #region Serrure
        private void Unlock(GamePlayer player, byte response)
        {
            if (response == 0x00)
            {
                m_interactPlayer = null;
                return;
            }

            RegionTimer timer = new RegionTimer(player, UnlockCallback);
            timer.Properties.setProperty("X", player.Position.X);
            timer.Properties.setProperty("Y", player.Position.Y);
            timer.Properties.setProperty("Z", player.Position.Z);
            timer.Properties.setProperty("Head", (int)player.Heading);
            timer.Properties.setProperty("player", player);
            timer.Start(500);
            player.Out.SendTimerWindow("Crochetage", UNLOCK_TIME);
        }

        private int UnlockCallback(RegionTimer timer)
        {
            m_interactPlayer = null;
            GamePlayer player = timer.Properties.getProperty<GamePlayer>("player", null);
            int Xpos = timer.Properties.getProperty("X", 0);
            int Ypos = timer.Properties.getProperty("Y", 0);
            int Zpos = timer.Properties.getProperty("Z", 0);
            int Head = timer.Properties.getProperty("Head", 0);
            if (player == null)
                return 0;
            if (Xpos != (int)player.Position.X || Ypos != (int)player.Position.Y || Zpos != (int)player.Position.Z || Head != player.Heading || player.InCombat)
            {
                player.Out.SendCloseTimerWindow();
                return 0;
            }

            int time = timer.Properties.getProperty("time", 0) + 500;
            timer.Properties.setProperty("time", time);
            if (time < UNLOCK_TIME * 1000)
                return 500;
            player.Out.SendCloseTimerWindow();

            int Chance = 100 - LockDifficult;

            //Dexterité
            float dextChance = (float)(player.Dexterity) / 125;
            if (dextChance > 1.0f)
                dextChance = 1.0f;
            if (dextChance < 0.1f)
                dextChance = 0.1f;
            Chance = (int)(dextChance * Chance);

            //Races
            switch (player.RaceName)
            {
                case "Half Ogre":
                case "Troll":
                    Chance -= 2;
                    break;

                case "Highlander":
                case "Firbolg":
                case "Dwarf":
                case "Norseman":
                    Chance -= 1;
                    break;

                case "Elf":
                    Chance += 1;
                    break;
            }

            //Classes
            switch (player.CharacterClass.ID)
            {
                case (int)eCharacterClass.AlbionRogue:
                case (int)eCharacterClass.Stalker:
                case (int)eCharacterClass.MidgardRogue:
                    Chance += 1;
                    break;

                case (int)eCharacterClass.Infiltrator:
                case (int)eCharacterClass.Minstrel:
                case (int)eCharacterClass.Scout:
                case (int)eCharacterClass.Hunter:
                case (int)eCharacterClass.Shadowblade:
                case (int)eCharacterClass.Nightshade:
                case (int)eCharacterClass.Ranger:
                    Chance += 2;
                    break;
            }

            if (Chance >= 100)
                Chance = 99;

            if (player.Client.Account.PrivLevel >= 2)
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.PickLockChance") + Chance + "/100", eChatType.CT_Important, eChatLoc.CL_SystemWindow);

            if (Chance > 0 && Util.Chance(Chance))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.PickLockSuccess"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                InteractEnd(player);
            }
            else
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameChest.PickLockFail"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                player.Inventory.RemoveTemplate(CROCHET, 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
            }
            return 0;
        }
        #endregion

        #region Item aléatoire
        private CoffreItem GetRandomItem(GamePlayer player)
        {
            // Calculate the combined chance
            int combinedChance = ItemChance;
            if (player != null)
            {
                // Add the player's loot chance to the combined chance
                combinedChance += player.LootChance / 2;
            }
            combinedChance = Math.Min(combinedChance, 100);

            if (!Util.Chance(combinedChance))
                return new CoffreItem("", 0);

            int num = Util.Random(1, AllChance);
            int i = 0;
            foreach (CoffreItem item in m_Items)
            {
                i += item.Chance;
                if (i >= num)
                    return item;
            }
            return new CoffreItem("", 0);
        }
        #endregion

        #region Gestion des items
        /// <summary>
        /// Ajoute ou modifie la chance d'apparition d'un item
        /// </summary>
        /// <param name="Id_nb">Id_nb de l'item à modifier ou ajouter</param>
        /// <param name="chance">Nombre de chance d'apparition de l'item</param>
        /// <returns>Retourne false si l'item n'existe pas dans la base de donné ItemTemplate</returns>
        public bool ModifyItemList(string Id_nb, int chance)
        {
            ItemTemplate item = GameServer.Database.SelectObject<ItemTemplate>(DB.Column("Id_nb").IsEqualTo(GameServer.Database.Escape(Id_nb)));
            if (item == null)
                return false;

            foreach (CoffreItem it in m_Items)
            {
                if (it.Id_nb == Id_nb)
                {
                    it.Chance = chance;
                    return true;
                }
            }

            m_Items.Add(new CoffreItem(Id_nb, chance));
            return true;
        }

        /// <summary>
        /// Supprime un item de la liste des items
        /// </summary>
        /// <param name="Id_nb">item à supprimer</param>
        /// <returns>Retourne true si l'item est supprimé</returns>
        public bool DeleteItemFromItemList(string Id_nb)
        {
            foreach (CoffreItem item in m_Items)
            {
                if (item.Id_nb == Id_nb)
                {
                    m_Items.Remove(item);
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Database
        public override void LoadFromDatabase(DataObject obj)
        {
            DBCoffre coffre = obj as DBCoffre;
            if (coffre == null) return;
            Name = coffre.Name;
            Position = Position.Create(coffre.Region, coffre.X, coffre.Y, coffre.Z, coffre.Heading);
            HasPickableAnim = coffre.HasPickableAnim;
            Model = coffre.Model;
            LastOpen = coffre.LastOpen;
            RespawnInterval = coffre.ItemInterval * 60;
            InternalID = coffre.ObjectId;
            ItemChance = coffre.ItemChance;
            KeyItem = coffre.KeyItem;
            LockDifficult = coffre.LockDifficult;
            Coffre = coffre;
            TrapRate = coffre.TrapRate;
            NpctemplateId = coffre.NpctemplateId;
            TpX = coffre.TpX;
            TpY = coffre.TpY;
            TpZ = coffre.TpZ;
            TPHeading = coffre.TPHeading;
            PunishSpellId = coffre.PunishSpellId;
            TpLevelRequirement = coffre.TpLevelRequirement;
            TpIsRenaissance = coffre.TpIsRenaissance;
            IsOpeningRenaissanceType = coffre.IsOpeningRenaissanceType;
            IsTeleporter = coffre.IsTeleporter;
            TpEffect = coffre.TpEffect;
            TpRegion = coffre.TpRegion;
            EventID = coffre.EventID;
            CoffreOpeningInterval = coffre.CoffreOpeningInterval;
            IsLargeCoffre = coffre.IsLargeCoffre;
            RemovedByEventID = coffre.RemovedByEventID;
            TPID = coffre.TPID;
            ShouldRespawnToTPID = coffre.ShouldRespawnToTPID;
            CurrentStep = coffre.CurrentStep;
            PickOnTouch = coffre.PickOnTouch;
            IsOpenableOnce = coffre.IsOpenableOnce;
            IsTerritoryLinked = coffre.IsTerritoryLinked;
            KeyLoseDur = coffre.KeyLoseDur;
            SwitchFamily = coffre.SwitchFamily;
            SwitchOrder = coffre.SwitchOrder;
            IsSwitch = coffre.IsSwitch;
            WrongOrderResetFamily = coffre.WrongOrderResetFamily;
            ActivatedDuration = coffre.ActivatedDuration;
            SecondaryModel = coffre.SecondaryModel;
            ActivatedBySwitchOn = coffre.ActivatedBySwitchOn;
            ActivatedBySwitchOff = coffre.ActivatedBySwitchOff;
            ResetBySwitchOn = coffre.ResetBySwitchOn;
            ResetBySwitchOff = coffre.ResetBySwitchOff;
            SwitchOnSound = coffre.SwitchOnSound;
            WrongFamilyOrderSound = coffre.WrongFamilyOrderSound;
            ActivatedFamilySound = coffre.ActivatedFamilySound;
            DeactivatedFamilySound = coffre.DeactivatedFamilySound;

            if (!string.IsNullOrEmpty(coffre.LootGenerator))
            {
                LootGenerators = new List<ILootGenerator>();
                foreach (var id in coffre.LootGenerator.Split(';'))
                {
                    var dbLootGenerator = GameServer.Database.SelectObject<LootGenerator>(DB.Column("LootGenerator_ID").IsEqualTo(id));
                    if (dbLootGenerator != null)
                    {
                        var lootGenerator = LootMgr.GetGeneratorInCache(dbLootGenerator);
                        if (lootGenerator != null)
                        {
                            LootGenerators.Add(lootGenerator);
                        }
                        else
                        {
                            log.Warn($"Loot generator {dbLootGenerator.ObjectId} not found in LootMgr cache for chest {InternalID}");
                        }
                    }
                    else
                    {
                        log.Warn($"No loot generator with ID {coffre.LootGenerator} found in DB for chest {InternalID}");
                    }
                }
            }
            else
                LootGenerators = null;

            m_Items = new List<CoffreItem>();
            if (coffre.ItemList != "")
                foreach (string item in coffre.ItemList.Split(';'))
                    m_Items.Add(new CoffreItem(item));
        }

        public override void SaveIntoDatabase()
        {
            if (Coffre == null)
                Coffre = new DBCoffre();

            Coffre.Name = Name;
            Coffre.X = (int)Position.X;
            Coffre.Y = (int)Position.Y;
            Coffre.Z = (int)Position.Z;
            Coffre.Heading = Heading;
            Coffre.Region = CurrentRegionID;
            Coffre.Model = Model;
            Coffre.LastOpen = LastOpen;
            Coffre.ItemInterval = RespawnInterval / 60;
            Coffre.ItemChance = ItemChance;
            Coffre.KeyItem = KeyItem;
            Coffre.LockDifficult = LockDifficult;
            Coffre.TrapRate = TrapRate;
            Coffre.NpctemplateId = NpctemplateId;
            Coffre.HasPickableAnim = HasPickableAnim;
            Coffre.TpX = TpX;
            Coffre.TpY = TpY;
            Coffre.TpZ = TpZ;
            Coffre.TPHeading = TPHeading;
            Coffre.PunishSpellId = PunishSpellId;
            Coffre.TpLevelRequirement = TpLevelRequirement;
            Coffre.TpIsRenaissance = TpIsRenaissance;
            Coffre.IsTeleporter = IsTeleporter;
            Coffre.TpEffect = TpEffect;
            Coffre.TpRegion = TpRegion;
            Coffre.IsOpeningRenaissanceType = IsOpeningRenaissanceType;
            Coffre.EventID = EventID;
            Coffre.CoffreOpeningInterval = CoffreOpeningInterval;
            Coffre.IsLargeCoffre = IsLargeCoffre;
            Coffre.RemovedByEventID = RemovedByEventID;
            Coffre.TPID = TPID;
            Coffre.ShouldRespawnToTPID = ShouldRespawnToTPID;
            Coffre.CurrentStep = CurrentStep;
            Coffre.PickOnTouch = PickOnTouch;
            Coffre.IsOpenableOnce = IsOpenableOnce;
            Coffre.IsTerritoryLinked = IsTerritoryLinked;
            Coffre.KeyLoseDur = KeyLoseDur;
            Coffre.SwitchFamily = SwitchFamily;
            Coffre.SwitchOrder = SwitchOrder;
            Coffre.IsSwitch = IsSwitch;
            Coffre.WrongOrderResetFamily = WrongOrderResetFamily;
            Coffre.ActivatedDuration = ActivatedDuration;
            Coffre.SecondaryModel = SecondaryModel;
            Coffre.ActivatedBySwitchOn = ActivatedBySwitchOn;
            Coffre.ActivatedBySwitchOff = ActivatedBySwitchOff;
            Coffre.ResetBySwitchOn = ResetBySwitchOn;
            Coffre.ResetBySwitchOff = ResetBySwitchOff;
            Coffre.SwitchOnSound = SwitchOnSound;
            Coffre.WrongFamilyOrderSound = WrongFamilyOrderSound;
            Coffre.ActivatedFamilySound = ActivatedFamilySound;
            Coffre.DeactivatedFamilySound = DeactivatedFamilySound;
            Coffre.LootGenerator = LootGenerators != null ? String.Join(';', LootGenerators.Select(g => g.DatabaseId)) : String.Empty;

            if (Items != null)
            {
                Coffre.ItemList = String.Join(';', Items.Select(item => item.Id_nb + "|" + item.Chance));
            }

            if (InternalID == null)
            {
                GameServer.Database.AddObject(Coffre);
                InternalID = Coffre.ObjectId;
            }
            else
                GameServer.Database.SaveObject(Coffre);
        }

        public override void DeleteFromDatabase()
        {
            if (Coffre == null)
                return;
            GameServer.Database.DeleteObject(Coffre);
        }
        #endregion

        #region CoffreItem
        public class CoffreItem
        {
            public string Id_nb;
            public int Chance;

            public CoffreItem(string id_nb, int chance)
            {
                Id_nb = id_nb;
                Chance = chance;
            }

            public CoffreItem(string item)
            {
                string[] values = item.Split('|');
                if (values.Length < 2)
                    throw new Exception("Pas de caractère séparateur pour l'item \"" + item + "\"");
                Id_nb = values[0];
                try
                {
                    Chance = int.Parse(values[1]);
                }
                catch
                {
                    Chance = 0;
                }
            }
        }
        #endregion

        public IList<string> DelveInfo()
        {
            List<string> text = new List<string>
                {
                    " + OID: " + ObjectID,
                    " + Class: " + GetType(),
                    " + Position: " + Position + " Heading=" + Heading,
                    " + Realm: " + Realm,
                    " + Model: " + Model,
                    " + EventID: " + EventID,
                    " + Masqué par un EventID: " + RemovedByEventID,
                    "",
                    "-- Coffre --",
                    " + Chance d'apparition d'un item: " + ItemChance + "%",
                    " + Intervalle d'apparition d'un item: " + RespawnInterval / 60 + " minutes",
                    " + Intervalle d'ouverture un coffre: " + this.CoffreOpeningInterval + " minutes",
                    " + Dernière fois que le coffre a été ouvert: " + LastOpen.ToShortDateString() + " " + LastOpen.ToShortTimeString(),
                    " + IsLongDistance type: " + this.IsLargeCoffre,
                    " + Respawn to TPID: " + ShouldRespawnToTPID,
                    " + TPID: " + TPID,
                    " + Current TPPoint step: " + CurrentStep,
                    " + Pick on Touch: " + PickOnTouch,
                    " + Secondary Model: " + SecondaryModel,
                    " + Is Openable Once: " + IsOpenableOnce,
                    " + Is Territory Linked: " + IsTerritoryLinked,
                    " + KeyLoseDur: " + KeyLoseDur, 
                    " + LootGenerators: "
                };
            if (LootGenerators != null)
                text.AddRange(LootGenerators.Select(g => "   - " + g.DatabaseId));
            if (LockDifficult > 0)
                text.Add(" + Difficulté pour crocheter le coffre: " + LockDifficult + "%");
            else
                text.Add(" + Ce coffre ne peut pas être crocheté");
            if (KeyItem != "")
                text.Add(" + Id_nb de la clef: " + KeyItem);
            else
                text.Add(" + Le coffre n'a pas besoin de clef");

            text.Add(" + PickableAnim: " + HasPickableAnim);

            text.Add("");
            text.Add(" + Listes des items (" + Items.Count + " items):");
            int i = 0;
            int TotalChance = 0;
            foreach (CoffreItem item in Items)
            {
                i++;
                TotalChance += item.Chance;
                text.Add("  " + i + ". " + item.Id_nb + " - " + item.Chance);
            }
            text.Add("Total des chances: " + TotalChance);

            text.Add("");
            text.Add("-- Teleport Info --");
            text.Add("IsTeleporter: " + this.IsTeleporter);
            text.Add("TP Level Requirement: " + this.TpLevelRequirement);
            text.Add("TP Effect: " + this.TpEffect);
            text.Add("X: " + TpX);
            text.Add("Y: " + TpY);
            text.Add("Z: " + TpZ);
            text.Add("TP Heading: " + this.TPHeading);
            text.Add("RegionID: " + TpRegion);

            text.Add("");
            text.Add("-- Trap Info --");
            text.Add("NPCTemplate: " + (this.NpctemplateId != null ? this.NpctemplateId : "-"));
            text.Add("Trap Rate: " + this.TrapRate);

            text.Add("");
            text.Add("-- Is Renaissance Info --");
            text.Add("IsOpeningRenaissanceType: " + this.IsOpeningRenaissanceType);
            text.Add("TpIsRenaissance: " + this.TpIsRenaissance);
            text.Add("PunishSpellId: " + this.PunishSpellId);

            text.Add("");
            text.Add("-- Is Switch Info --");
            text.Add("IsSwitch: " + this.IsSwitch);
            text.Add("Switch Family: " + this.SwitchFamily);
            text.Add("Switch Order: " + this.SwitchOrder);
            text.Add("Reset Switch if wrong Order: " + this.WrongOrderResetFamily);
            text.Add("Activated Duration: " + this.ActivatedDuration + " secondes");
            text.Add("Switch ON activates EventID: " + this.ActivatedBySwitchOn);
            text.Add("Switch OFF deactivates EventID: " + this.ActivatedBySwitchOff);
            text.Add("Switch ON resets EventID: " + this.ResetBySwitchOn);
            text.Add("Switch OFF resets EventID: " + this.ResetBySwitchOff);
            text.Add("Switch ON Sound Effect: " + this.SwitchOnSound);
            text.Add("Wrong Family Order Sound Effect: " + this.WrongFamilyOrderSound);
            text.Add("Switch Activated by Family Sound Effect: " + this.ActivatedFamilySound);
            text.Add("Switch Family Timer ENDS Sound Effect: " + this.DeactivatedFamilySound);
            return text;
        }

        public override void CustomCopy(GameObject source)
        {
            base.CustomCopy(source);
            GameCoffre coffre = source as GameCoffre;
            if (coffre != null)
            {
                m_Items = coffre.Items;
                Name = coffre.Name + "_cpy";
                RespawnInterval = coffre.RespawnInterval;
                TpEffect = coffre.TpEffect;
                TpIsRenaissance = coffre.TpIsRenaissance;
                TpLevelRequirement = coffre.TpLevelRequirement;
                TpX = coffre.TpX;
                TpY = coffre.TpY;
                TpZ = coffre.TpZ;
                TpRegion = coffre.TpRegion;
                TrapRate = coffre.TrapRate;
                NpctemplateId = coffre.NpctemplateId;
                PunishSpellId = coffre.PunishSpellId;
                IsOpeningRenaissanceType = coffre.IsOpeningRenaissanceType;
                IsTeleporter = coffre.IsTeleporter;
                CoffreOpeningInterval = coffre.CoffreOpeningInterval;
                IsLargeCoffre = coffre.IsLargeCoffre;
                ItemChance = coffre.ItemChance;
                KeyItem = coffre.KeyItem;
                TPID = coffre.TPID;
                ShouldRespawnToTPID = coffre.ShouldRespawnToTPID;
                CurrentStep = coffre.CurrentStep;
                PickOnTouch = coffre.PickOnTouch;
                SecondaryModel = coffre.SecondaryModel;
                IsOpenableOnce = coffre.IsOpenableOnce;
                IsTerritoryLinked = coffre.IsTerritoryLinked;
                KeyLoseDur = coffre.KeyLoseDur;
                SwitchFamily = coffre.SwitchFamily;
                SwitchOrder = coffre.SwitchOrder;
                IsSwitch = coffre.IsSwitch;
                WrongOrderResetFamily = coffre.WrongOrderResetFamily;
                ActivatedDuration = coffre.ActivatedDuration;
                ActivatedBySwitchOn = coffre.ActivatedBySwitchOn;
                ActivatedBySwitchOff = coffre.ActivatedBySwitchOff;
                ResetBySwitchOn = coffre.ResetBySwitchOn;
                ResetBySwitchOff = coffre.ResetBySwitchOff;
                SwitchOnSound = coffre.SwitchOnSound;
                WrongFamilyOrderSound = coffre.WrongFamilyOrderSound;
                ActivatedFamilySound = coffre.ActivatedFamilySound;
                DeactivatedFamilySound = coffre.DeactivatedFamilySound;
            }
        }
    }
}