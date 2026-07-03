using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;

namespace DOL.GS.Scripts
{
    public class GenistarEgg : GameNPC
    {
        public DBGenistar DBRecord { get; private set; }
        private RegionTimer m_careTimer;

        public GenistarEgg() : base()
        {
            SetOwnBrain(new BlankBrain());
            this.SaveInDB = false;
        }

        public override int Health { get { return base.MaxHealth; } set { } }
        public override bool IsAlive { get { return true; } }
        public override void OnAttackedByEnemy(AttackData ad) { }

        public void LoadFromGenistarDB(DBGenistar record)
        {
            DBRecord = record;
            Name = "Incubating Genistar";
            Model = 667;
            Size = 80;
            Flags |= eFlags.PEACE;
            Level = 1;
            MaxSpeedBase = 0;
            Realm = eRealm.None;

            m_careTimer = new RegionTimer(this, new RegionTimerCallback(CareTimerTick));
            m_careTimer.Start(5000);
        }

        public override bool Interact(GamePlayer player)
        {
            string lang = player.Client?.Account?.Language ?? "EN";

            if (DBRecord == null || player.InternalID != DBRecord.OwnerID)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarEgg.SealRejects"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (DBRecord.RemainingIncubationTime <= 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarEgg.StasisCollapsed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                Hatch(player);
            }
            else
            {
                int remainingSeconds = Math.Max(0, DBRecord.RemainingIncubationTime / 1000);
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarEgg.StasisActive", remainingSeconds), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

            return base.Interact(player);
        }

        private int CareTimerTick(RegionTimer timer)
        {
            if (DBRecord == null || DBRecord.State != (int)eGenistarState.Incubating) 
                return 0;

            // 1. Check if it finished an un-hatched cycle while sitting there
            if (DBRecord.RemainingIncubationTime <= 0)
            {
                GamePlayer presentOwner = WorldMgr.GetClientByPlayerID(DBRecord.OwnerID, true, false)?.Player;
                if (presentOwner != null && presentOwner.IsWithinRadius(this, 150))
                {
                    Hatch(presentOwner);
                    return 0;
                }
                return 5000;
            }

            GamePlayer owner = WorldMgr.GetClientByPlayerID(DBRecord.OwnerID, true, false)?.Player;

            // 2. If offline, dead, or left the housing zone, freeze math entirely.
            if (owner == null || owner.CurrentRegionID != this.CurrentRegionID || !owner.IsAlive)
                return 5000;

            bool isAfkCaring = owner.TempProperties.getProperty<bool>("IsAfkCareMode", false);
            GameObject careTarget = owner.TempProperties.getProperty<GameObject>("AfkCareTarget", null);

            // 3. If the player moved, DAoC dropped their AFK flag; drop Care automatically.
            if (isAfkCaring && careTarget == this && owner.IsAfkActive())
            {
                if (owner.IsWithinRadius(this, 150))
                {
                    int tickReduction = 5000; // Standard 5s tick

                    if (owner.Client.Account.PrivLevel > 1) 
                    {
                        tickReduction *= 10; // GM 10x Speedup for testing
                    }

                    DBRecord.RemainingIncubationTime -= tickReduction;
                    if (DBRecord.RemainingIncubationTime < 0) 
                        DBRecord.RemainingIncubationTime = 0;

                    // Locate active Lens in top backpack slots (Flag == 27)
                    InventoryItem activeLens = null;
                    lock (owner.Inventory)
                    {
                        for (int i = (int)eInventorySlot.FirstBackpack; i <= (int)eInventorySlot.LastBackpack; i++)
                        {
                            var item = owner.Inventory.GetItem((eInventorySlot)i);
                            if (item != null && item.Template != null && item.Template.Flags == 27)
                            {
                                activeLens = item;
                                break;
                            }
                        }
                    }

                    if (activeLens != null)
                    {
                        // If a GM speeds it up 10x, the Lens gets 50,000ms of historical credit, keeping the recipe math true!
                        GenistarLensMgr.ApplyLensTick(DBRecord, activeLens as GameInventoryItem, tickReduction, owner.EruditionLevel, owner);

                        int effectMod = 7034 + (activeLens.DPS_AF % 10);
                        owner.Out.SendSpellEffectAnimation(owner, this, (ushort)effectMod, 0, false, 1);
                    }
                    else
                    {
                        owner.Out.SendSpellEffectAnimation(owner, this, 7034, 0, false, 1);
                    }

                    GameServer.Database.SaveObject(DBRecord);

                    if (DBRecord.RemainingIncubationTime <= 0)
                    {
                        owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarEgg.WeaveLocks"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        Hatch(owner);
                        return 0;
                    }
                }
                else
                {
                    owner.TempProperties.removeProperty("IsAfkCareMode");
                    owner.TempProperties.removeProperty("AfkCareTarget");
                    owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.GenistarEgg.TooFar"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }

            return 5000;
        }

        private void Hatch(GamePlayer player)
        {
            if (m_careTimer != null) 
            { 
                m_careTimer.Stop(); 
                m_careTimer = null; 
            }

            House house = HouseMgr.GetHouse(DBRecord.HouseNumber);
            if (house != null) house.UpdateGenistarVisual(DBRecord.PlaceholderKey, 1682);

            ChimeraEvaluator.ExecuteHatchSequence(player, DBRecord, this);
        }
    }
}