using DOL.Database;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using DOL.GS.Spells;
using DOL.MobGroups;
using DOL.Territories;
using DOLDatabase.Tables;
using System.Collections.Generic;
using System.Linq;

namespace AmteScripts.Managers
{
    public class TerritoryRelicStatic : GameStaticItem
    {
        public DBMinotaurRelic DbRecord { get; private set; }
        public bool IsLocked { get; private set; }
        public Territory CurrentTerritory { get; private set; }
        public TerRelicPadNPC CurrentPad { get; set; }
        public GamePlayer CurrentCarrier { get; set; }
        public bool IsDroppedOnGround { get; set; }
        public bool IsDungeonRelic { get; private set; }

        private RegionTimer _respawnTimer;
        private GameStaticItem _visualPad;
        private List<GameNPC> _activeProtectors = new List<GameNPC>();

        public TerritoryRelicStatic(DBMinotaurRelic record)
        {
            DbRecord = record;
            Name = record.Name;
            Model = record.Model;
            Level = 50;
            Realm = 0;
            Position = Position.Create((ushort)record.SpawnRegion, record.SpawnX, record.SpawnY, record.SpawnZ + 100, (ushort)record.SpawnHeading);

            var region = WorldMgr.GetRegion((ushort)record.SpawnRegion);
            if (region != null)
            {
                var zone = region.GetZone(record.SpawnX, record.SpawnY);
                IsDungeonRelic = zone != null && zone.IsDungeon;
            }
        }

        public override bool AddToWorld()
        {
            if (Position.X == DbRecord.SpawnX && Position.Y == DbRecord.SpawnY)
            {
                SpawnVisualPad();
            }

            bool result = base.AddToWorld();

            if (result)
            {
                if (Position.X == DbRecord.SpawnX && Position.Y == DbRecord.SpawnY)
                {
                    if (!string.IsNullOrEmpty(DbRecord.ProtectorClassType))
                    {
                        IsLocked = true;
                        SpawnProtectors();
                    }
                }
                TerritoryRelicManager.UpdateMapPins();
            }
            return result;
        }

        private void SpawnProtectors()
        {
            if (MobGroupManager.Instance.Groups.TryGetValue(DbRecord.ProtectorClassType, out MobGroup group))
            {
                foreach (var protector in group.NPCs)
                {
                    protector.RemoveFromWorld();

                    Position newPos = Position.Create(
                        (ushort)DbRecord.SpawnRegion,
                        DbRecord.SpawnX + Util.Random(100, 200),
                        DbRecord.SpawnY + Util.Random(100, 200),
                        DbRecord.SpawnZ,
                        (ushort)DbRecord.SpawnHeading
                    );

                    protector.Position = newPos;
                    protector.Home = newPos;
                    protector.SpawnPosition = newPos;

                    protector.AddToWorld();

                    if (!_activeProtectors.Contains(protector))
                        _activeProtectors.Add(protector);
                }
            }
        }

        private void SpawnVisualPad()
        {
            if (_visualPad != null) return;

            ushort padModel = (this.CurrentZone != null && this.CurrentZone.IsDungeon) ? (ushort)2655 : (ushort)3547;

            _visualPad = new GameStaticItem
            {
                Model = padModel,
                Name = "Outpost Relic Pad",
                Level = 50,
                Realm = 0,
                Position = this.Position.With(z: this.Position.Z - 100)
            };
            _visualPad.AddToWorld();
        }

        public void Unlock()
        {
            IsLocked = false;
            foreach (var p in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).OfType<GamePlayer>())
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client.Account.Language, "TerritoryRelics.Static.Unlocked", Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            if (!DOL.GS.Scripts.GvGManager.IsOpen)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.Truce"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.IsDamned)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.CannotPickupDamned"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (SpellHandler.FindEffectOnTarget(player, "Petrify") != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.CannotPickupPtrified"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            var wsd = SpellHandler.FindEffectOnTarget(player, "WarlockSpeedDecrease");
            if (wsd != null && wsd.Spell != null && wsd.Spell.AmnesiaChance == 1)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.CannotPickupFrog"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.ActiveBanner != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.CannotPickupGuildBanner"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (IsLocked)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.Protected"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (!player.IsAlive)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.CannotPickupDead"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.IsStealthed)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.CannotPickupStealthed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.InCombat)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.CannotPickupCombat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.IsOnHorse)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.CannotPickupMounted"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.PlayerAfkMessage != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.CannotPickupAFK"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.DuelTarget != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.CannotPickupDuel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.Guild == null || !TerritoryManager.Instance.Territories.Any(t => t.OwnerGuild == player.Guild))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.NeedTerritory"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
            {
                if (player.Inventory.GetItem(slot) is TerritoryRelicInventoryItem)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.OnlyOne"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            if (CurrentTerritory != null)
            {
                if (CurrentTerritory.IsOwnedBy(player))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.AlreadySecured"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                CurrentTerritory.RemoveRelicBonus(DbRecord.Bonuses, IsDungeonRelic);

                DbRecord.relicTarget = "carrier";
                GameServer.Database.SaveObject(DbRecord);

                CurrentTerritory = null;
                CurrentPad.CurrentRelic = null;
                CurrentPad = null;
            }

            var invItem = new TerritoryRelicInventoryItem(this);
            if (player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, invItem))
            {
                CurrentCarrier = player; // Track carrier
                IsDroppedOnGround = false;
                DbRecord.relicTarget = "carrier";
                GameServer.Database.SaveObject(DbRecord);

                RemoveFromWorld();
                if (_returnTimer != null) { _returnTimer.Stop(); _returnTimer = null; }
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Static.PickedUp", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return true;
            }

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.PickupObject.BackpackFull"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return false;
        }

        private RegionTimer _returnTimer;

        // When the drop timer expires, safely warp it back to spawn instead of deleting the outpost pad
        public void ReturnToSpawn()
        {
            if (CurrentCarrier != null)
            {
                var item = CurrentCarrier.Inventory.GetFirstItemByName(Name, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack) as TerritoryRelicInventoryItem;
                if (item != null)
                {
                    item.IsRemovalExpected = true;
                    CurrentCarrier.Inventory.RemoveItem(item);
                }
                CurrentCarrier = null;
            }

            if (CurrentTerritory != null)
            {
                CurrentTerritory.RemoveRelicBonus(DbRecord.Bonuses, IsDungeonRelic);
                CurrentTerritory = null;
            }
            if (CurrentPad != null)
            {
                CurrentPad.CurrentRelic = null;
                CurrentPad = null;
            }
            if (_returnTimer != null)
            {
                _returnTimer.Stop();
                _returnTimer = null;
            }

            IsDroppedOnGround = false;
            DbRecord.relicTarget = "outpost";
            GameServer.Database.SaveObject(DbRecord);

            RemoveFromWorld();

            Position = Position.Create((ushort)DbRecord.SpawnRegion, DbRecord.SpawnX, DbRecord.SpawnY, DbRecord.SpawnZ, (ushort)DbRecord.SpawnHeading);
            AddToWorld();
            TerritoryRelicManager.UpdateMapPins();
        }

        public void DropOnGround(int x, int y, int z, ushort heading, Region region)
        {
            CurrentCarrier = null;
            IsDroppedOnGround = true;
            DbRecord.relicTarget = "outpost";
            GameServer.Database.SaveObject(DbRecord);

            Position = Position.Create(region.ID, x, y, z, heading);
            AddToWorld();

            _returnTimer = new RegionTimer(this, t =>
            {
                ReturnToSpawn(); // Don't Destroy(), return to spawn instead
                return 0;
            });

            int dropDuration = Properties.GVG_RELIC_DROP_DURATION > 0 ? Properties.GVG_RELIC_DROP_DURATION : 30;
            _returnTimer.Start(dropDuration * 1000);
        }

        public void AttachToTerritory(Territory territory, TerRelicPadNPC pad)
        {
            CurrentCarrier = null;
            IsDroppedOnGround = false;
            CurrentTerritory = territory;
            CurrentPad = pad;
            Position = pad.Position;

            DbRecord.relicTarget = pad.InternalID;
            GameServer.Database.SaveObject(DbRecord);

            AddToWorld();

            CurrentTerritory.AddRelicBonus(DbRecord.Bonuses, IsDungeonRelic);
            TerritoryRelicManager.UpdateMapPins();
        }

        public void Destroy()
        {
            if (CurrentCarrier != null)
            {
                var item = CurrentCarrier.Inventory.GetFirstItemByName(Name, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack) as TerritoryRelicInventoryItem;
                if (item != null)
                {
                    item.IsRemovalExpected = true;
                    CurrentCarrier.Inventory.RemoveItem(item);
                }
                CurrentCarrier = null;
            }

            if (CurrentTerritory != null)
            {
                CurrentTerritory.RemoveRelicBonus(DbRecord.Bonuses, IsDungeonRelic);
                CurrentTerritory = null;
            }
            if (CurrentPad != null)
            {
                CurrentPad.CurrentRelic = null;
                CurrentPad = null;
            }
            if (_visualPad != null)
            {
                _visualPad.RemoveFromWorld();
                _visualPad = null;
            }
            if (_returnTimer != null)
            {
                _returnTimer.Stop();
                _returnTimer = null;
            }

            foreach (var protector in _activeProtectors)
            {
                if (protector.ObjectState == eObjectState.Active)
                    protector.RemoveFromWorld();
            }
            _activeProtectors.Clear();

            DbRecord.relicTarget = "inactive";
            GameServer.Database.SaveObject(DbRecord);

            RemoveFromWorld();
        }

        public override void Delete()
        {
            if (_returnTimer != null) { _returnTimer.Stop(); _returnTimer = null; }
            if (_visualPad != null) _visualPad.RemoveFromWorld();
            base.Delete();
        }
    }
}