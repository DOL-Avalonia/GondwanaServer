using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace DOL.GS.Scripts
{
    public class GenistarLifecycleManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);
        private static Timer m_cleanupTimer;

        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            m_cleanupTimer = new Timer(CleanupRoutine, null, 15 * 60 * 1000, 15 * 60 * 1000);
            GenistarLensMgr.InitializeLenses();
        }

        [ScriptUnloadedEvent]
        public static void OnScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            if (m_cleanupTimer != null)
            {
                m_cleanupTimer.Dispose();
                m_cleanupTimer = null;
            }
        }

        private static void CleanupRoutine(object state)
        {
            try
            {
                long currentTime = GameTimer.GetTickCount();
                IList<DBGenistar> deadGenistars = GameServer.Database.SelectObjects<DBGenistar>(
                    DB.Column("State").IsEqualTo((int)eGenistarState.Dead).Or(DB.Column("State").IsEqualTo((int)eGenistarState.Recovering)));

                foreach (DBGenistar gen in deadGenistars)
                {
                    if (gen.TimerEnd > 0 && currentTime > gen.TimerEnd)
                    {
                        GamePlayer owner = WorldMgr.GetClientByPlayerID(gen.OwnerID, true, false)?.Player;
                        if (owner != null)
                        {
                            owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "Genistar.Lifecycle.Degraded"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

                            InventoryItem remainsItem = null;
                            lock (owner.Inventory)
                            {
                                for (int i = (int)eInventorySlot.FirstBackpack; i <= (int)eInventorySlot.LastBackpack; i++)
                                {
                                    var item = owner.Inventory.GetItem((eInventorySlot)i);
                                    if (item != null && item.Id_nb.StartsWith("genistar_remains") && item.PackageID == gen.GenistarID)
                                    {
                                        remainsItem = item;
                                        break;
                                    }
                                }
                            }
                            if (remainsItem != null)
                            {
                                ItemUnique uT = remainsItem.Template as ItemUnique;
                                owner.Inventory.RemoveItem(remainsItem);
                                if (uT != null) GameServer.Database.DeleteObject(uT);
                            }
                        }
                        else
                        {
                            var offlineItems = GameServer.Database.SelectObjects<InventoryItem>(
                                DB.Column("OwnerID").IsEqualTo(gen.OwnerID).And(
                                DB.Column("PackageID").IsEqualTo(gen.GenistarID)));

                            foreach (var item in offlineItems)
                            {
                                if (item.Id_nb.StartsWith("genistar_remains") || item.Id_nb.StartsWith("genistar_pet"))
                                {
                                    ItemUnique uT = item.Template as ItemUnique;
                                    GameServer.Database.DeleteObject(item);
                                    if (uT != null) GameServer.Database.DeleteObject(uT);
                                }
                            }
                        }

                        var orphanedUniques = GameServer.Database.SelectObjects<ItemUnique>(DB.Column("PackageID").IsEqualTo(gen.GenistarID));
                        foreach (var uT in orphanedUniques)
                        {
                            GameServer.Database.DeleteObject(uT);
                        }

                        House house = HouseMgr.GetHouse(gen.HouseNumber);
                        if (house != null)
                        {
                            house.UpdateGenistarVisual(gen.PlaceholderKey, 1293);
                        }

                        GameServer.Database.DeleteObject(gen);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Error in GenistarLifecycleManager.CleanupRoutine", ex);
            }
        }

        public static void ProcessEmbryoCreation(GamePlayer player, int houseNumber, string baseTemplateId, int eruditionLevel, int dictKey)
        {
            string lang = player.Client?.Account?.Language ?? "EN";
            int parsedTemplateId = 1500;
            string digits = new string(baseTemplateId.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrEmpty(digits))
            {
                int.TryParse(digits, out parsedTemplateId);
            }

            int failureChance = CalculateFailureChance(eruditionLevel, parsedTemplateId);

            if (Util.Chance(failureChance))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Lifecycle.EmbryoFailed"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                House house = HouseMgr.GetHouse(houseNumber);
                if (house != null) house.UpdateGenistarVisual(dictKey, 1293);
                return;
            }

            INpcTemplate baseTmpl = NpcTemplateMgr.GetTemplate(parsedTemplateId);
            int initialMaxTension = baseTmpl != null ? baseTmpl.MaxTension : 3000;

            DBGenistar newGenistar = new DBGenistar();
            newGenistar.GenistarID = Guid.NewGuid().ToString();
            newGenistar.OwnerID = player.InternalID;
            newGenistar.HouseNumber = houseNumber;
            newGenistar.PlaceholderKey = dictKey;
            newGenistar.BaseTemplateID = parsedTemplateId;
            newGenistar.EruditionAtBirth = eruditionLevel;
            newGenistar.State = (int)eGenistarState.Incubating;
            newGenistar.RemainingIncubationTime = 3600000;
            newGenistar.TimerEnd = 0;
            newGenistar.MaxTension = initialMaxTension;
            newGenistar.CurrentTension = 0;

            GameServer.Database.AddObject(newGenistar);

            House targetHouse = HouseMgr.GetHouse(houseNumber);
            if (targetHouse != null)
            {
                GenistarEgg egg = new GenistarEgg();

                if (targetHouse.GenistarVisuals.TryGetValue(dictKey, out var visual) && visual != null)
                    egg.Position = visual.Position;
                else
                    egg.Position = targetHouse.GetGenistarSlotPosition(dictKey, null);

                egg.LoadFromGenistarDB(newGenistar);
                egg.AddToWorld();
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Lifecycle.IncubationBegun"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        public static void ProcessRemainsRecovery(GamePlayer player, string genistarID, House house, int dictKey)
        {
            string lang = player.Client?.Account?.Language ?? "EN";
            DBGenistar deadRecord = GameServer.Database.SelectObject<DBGenistar>(DB.Column("GenistarID").IsEqualTo(genistarID));

            if (deadRecord != null && deadRecord.State == (int)eGenistarState.Dead && deadRecord.OwnerID == player.InternalID)
            {

                deadRecord.State = (int)eGenistarState.Recovering;
                deadRecord.TimerEnd = GameTimer.GetTickCount() + (24 * 60 * 60 * 1000);
                GameServer.Database.SaveObject(deadRecord);

                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Lifecycle.RecoveryStarted"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                house.UpdateGenistarVisual(dictKey, 1682);

                GenistarNPC dormantPet = new GenistarNPC();

                if (house.GenistarVisuals.TryGetValue(dictKey, out var visual) && visual != null)
                    dormantPet.Position = visual.Position;
                else
                    dormantPet.Position = house.GetGenistarSlotPosition(dictKey, null);

                dormantPet.LoadFromGenistarDB(deadRecord);
                dormantPet.AddToWorld();
            }
            else
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Lifecycle.RemainsInert"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        public static void ExecuteTotalVaporization(GamePlayer player, InventoryItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.PackageID)) return;
            if (!item.Id_nb.StartsWith("genistar_pet") && !item.Id_nb.StartsWith("genistar_remains")) return;

            string targetGenistarId = item.PackageID;

            DBGenistar gen = GameServer.Database.SelectObject<DBGenistar>(DB.Column("GenistarID").IsEqualTo(targetGenistarId));
            if (gen != null)
            {
                int houseNum = gen.HouseNumber;
                int placeholderKey = gen.PlaceholderKey;
                string creatureName = !string.IsNullOrEmpty(gen.CustomName) ? gen.CustomName : gen.Name;

                GameServer.Database.DeleteObject(gen);

                House house = HouseMgr.GetHouse(houseNum);
                if (house != null)
                {
                    house.UpdateGenistarVisual(placeholderKey, 1293);
                }

                string lang = player?.Client?.Account?.Language ?? "EN";
                player?.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Lifecycle.Purged", creatureName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            var orphanedUniques = GameServer.Database.SelectObjects<ItemUnique>(DB.Column("PackageID").IsEqualTo(targetGenistarId));
            if (orphanedUniques != null)
            {
                foreach (var uT in orphanedUniques)
                {
                    GameServer.Database.DeleteObject(uT);
                }
            }
        }

        private static int CalculateFailureChance(int erudition, int templateId)
        {
            int complexityTier = GetTierFromTemplate(templateId);
            int diff = complexityTier - erudition;

            if (diff <= 0) return 5; // Base 5% failure even if you are a master

            return 5 + (diff * 20); // 20% penalty for each tier you lack.
        }

        public static int GetTierFromTemplate(int templateId)
        {
            return templateId switch
            {
                1500 or 1501 or 1502 or 1503 => 0, // Worm, Frog, Snake, Spider
                1510 => 1, // Lizard
                1504 or 1507 or 1508 or 1509 => 3, // Wildcat, Bear, Boar, Wolf
                1505 => 5, // Golem
                1506 => 7, // Giants
                _ => 0
            };
        }
    }
}