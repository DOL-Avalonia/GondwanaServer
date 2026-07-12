using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AmteScripts.PvP.Rewards
{
    public enum eRewardTier
    {
        RvRFinest,
        PvPTier1,
        PvPTier2,
        PvPTier3
    }

    public enum eRewardChestType
    {
        ScrollsAndBuffs,
        ArmorsAndWeapons,
        Jewellery
    }

    public class RewardChest : GameCoffre
    {
        public new string OwnerID { get; set; }
        public eRewardTier Tier { get; set; }
        public eRewardChestType ChestType { get; set; }

        public int ConfigTargetSlots { get; set; } = 8;
        public double ConfigTargetMaxUti { get; set; } = 85;
        public int ConfigRegionID { get; set; } = 230;
        public string ConfigRarityPrefix { get; set; } = "Rare";
        public int ConfigItemColor { get; set; } = 18;

        public List<RewardChest> Siblings { get; set; } = new List<RewardChest>();

        public RewardChest() : base()
        {
            IsOpenableOnce = false;
            RespawnInterval = 0;
            PickOnTouch = false;
            InternalID = Guid.NewGuid().ToString();
        }

        public override void SaveIntoDatabase() { }
        public override void DeleteFromDatabase() { }

        public override bool IsVisibleTo(GameObject checkObject)
        {
            if (checkObject == null) return false;

            if (checkObject is GamePlayer player)
            {
                if (player.Client?.Account?.PrivLevel > 1)
                    return base.IsVisibleTo(checkObject);

                if (OwnerID == player.InternalID)
                    return base.IsVisibleTo(checkObject);

                return false;
            }

            return base.IsVisibleTo(checkObject);
        }

        public override bool Interact(GamePlayer player)
        {
            if (OwnerID != player.InternalID && player.Client?.Account?.PrivLevel <= 1)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Lootgenerator.RewardChests.NotYourChest"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            int emptySlots = 0;
            for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
            {
                if (player.Inventory.GetItem(slot) == null)
                {
                    emptySlots++;
                    break;
                }
            }

            if (emptySlots == 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client!.Account.Language, "Lootgenerator.RewardChests.InventoryFull"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return false; // Prevents the chest and its siblings from deleting
            }

            bool success = base.Interact(player);

            if (success)
            {
                foreach (var sibling in Siblings)
                {
                    if (sibling != null && sibling.ObjectState == GameObject.eObjectState.Active)
                    {
                        sibling.RemoveFromWorld();
                        sibling.Delete();
                    }
                }
                Siblings.Clear();

                this.RemoveFromWorld();
                this.Delete();
            }

            return success;
        }
    }

    public static class RewardChestSpawner
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        /// Spawns 3 instanced chests 160 units behind the teleporter for a winning player.
        /// </summary>
        public static bool SpawnChestsForPlayer(GamePlayer player, eRewardTier tier, GameNPC teleporter, int peakPlayers = 0, int score = 0, int neededPlayers = 0)
        {
            if (player == null) return false;

            if (Properties.PVP_REWARDS_USE_SUBTIERS)
            {
                if (!DetermineSubtierConfig(tier, peakPlayers, score, neededPlayers, out int targetSlots, out double targetMaxUti, out int regionID, out string rarityPrefix, out int color))
                {
                    log.Info($"[Reward System] Player {player.Name} did not meet subtier thresholds for {tier}. No chests spawned.");
                    return false;
                }

                SpawnWithConfig(player, tier, teleporter, targetSlots, targetMaxUti, regionID, rarityPrefix, color);
            }
            else
            {
                GetDefaultConfig(tier, out int targetSlots, out double targetMaxUti, out int regionID, out string rarityPrefix, out int color);
                SpawnWithConfig(player, tier, teleporter, targetSlots, targetMaxUti, regionID, rarityPrefix, color);
            }

            return true;
        }

        public static bool DetermineSubtierConfig(eRewardTier tier, int peakPlayers, int score, int neededPlayers, out int targetSlots, out double targetMaxUti, out int regionID, out string rarityPrefix, out int color)
        {
            targetSlots = 8;
            targetMaxUti = 85;
            regionID = 230;
            rarityPrefix = "Rare";
            color = 0;

            if (neededPlayers <= 0) neededPlayers = 10;

            int p = neededPlayers;
            int p50 = (int)(neededPlayers * 1.5);
            int p100 = neededPlayers * 2;
            int p150 = (int)(neededPlayers * 2.5);

            int subTier = 0;

            if (tier == eRewardTier.RvRFinest)
            {
                int sBase = p;
                int s50 = p50;
                int s100 = p100;

                if (peakPlayers >= p150 && score >= s100) subTier = 4;
                else if (peakPlayers >= p100 && score >= s100) subTier = 3;
                else if (peakPlayers >= p50 && score >= s50) subTier = 2;
                else if (peakPlayers >= p && score >= sBase) subTier = 1;

                switch (subTier)
                {
                    case 4: targetSlots = 11; targetMaxUti = 155; regionID = 230; rarityPrefix = "Mythical"; color = 26; break;
                    case 3: targetSlots = 11; targetMaxUti = 130; regionID = 230; rarityPrefix = "Exalted"; color = 26; break;
                    case 2: targetSlots = 11; targetMaxUti = 108; regionID = 230; rarityPrefix = "Fabled"; color = 26; break;
                    case 1: targetSlots = 10; targetMaxUti = 95; regionID = 230; rarityPrefix = "Legendary"; color = 22; break; // Downgrade mapping
                    default: return false;
                }
            }
            else if (tier == eRewardTier.PvPTier1)
            {
                int sBase = p;
                int s50 = p50;
                int s100 = p100;

                if (peakPlayers >= p150 && score >= s100) subTier = 4;
                else if (peakPlayers >= p100 && score >= s100) subTier = 3;
                else if (peakPlayers >= p50 && score >= s50) subTier = 2;
                else if (peakPlayers >= p && score >= sBase) subTier = 1;

                switch (subTier)
                {
                    case 4: targetSlots = 10; targetMaxUti = 99; regionID = 230; rarityPrefix = "Legendary"; color = 22; break;
                    case 3: targetSlots = 9; targetMaxUti = 92; regionID = 230; rarityPrefix = "Eminent"; color = 22; break;
                    case 2: targetSlots = 9; targetMaxUti = 85; regionID = 230; rarityPrefix = "Illustrious"; color = 22; break;
                    case 1: targetSlots = 8; targetMaxUti = 85; regionID = 233; rarityPrefix = "Epic"; color = 19; break; // Downgrade mapping
                    default: return false;
                }
            }
            else if (tier == eRewardTier.PvPTier2)
            {
                int sBase = (int)(p * 0.75);
                int s50 = (int)(p50 * 0.75);
                int s100 = (int)(p100 * 0.75);

                if (peakPlayers >= p150 && score >= s100) subTier = 4;
                else if (peakPlayers >= p100 && score >= s100) subTier = 3;
                else if (peakPlayers >= p50 && score >= s50) subTier = 2;
                else if (peakPlayers >= p && score >= sBase) subTier = 1;

                switch (subTier)
                {
                    case 4: targetSlots = 8; targetMaxUti = 95; regionID = 233; rarityPrefix = "Epic"; color = 19; break;
                    case 3: targetSlots = 8; targetMaxUti = 86; regionID = 233; rarityPrefix = "Superior"; color = 19; break;
                    case 2: targetSlots = 8; targetMaxUti = 82; regionID = 58; rarityPrefix = "Flawless"; color = 19; break;
                    case 1: targetSlots = 8; targetMaxUti = 75; regionID = 35; rarityPrefix = "Rare"; color = 18; break; // Downgrade mapping
                    default: return false;
                }
            }
            else if (tier == eRewardTier.PvPTier3)
            {
                int sBase = (int)(p * 0.50);
                int s50 = (int)(p50 * 0.50);
                int s100 = (int)(p100 * 0.50);

                if (peakPlayers >= p150 && score >= s100) subTier = 4;
                else if (peakPlayers >= p100 && score >= s100) subTier = 3;
                else if (peakPlayers >= p50 && score >= s50) subTier = 2;
                else if (peakPlayers >= p && score >= sBase) subTier = 1;

                switch (subTier)
                {
                    case 4: targetSlots = 8; targetMaxUti = 85; regionID = 35; rarityPrefix = "Rare"; color = 18; break;
                    case 3: targetSlots = 7; targetMaxUti = 80; regionID = 35; rarityPrefix = "Fine"; color = 18; break;
                    case 2: targetSlots = 7; targetMaxUti = 72; regionID = 30; rarityPrefix = "Uncommon"; color = 18; break;
                    case 1: return false; // Intentionally fail chest creation for lowest tier 3
                    default: return false;
                }
            }

            return true;
        }

        public static void GetDefaultConfig(eRewardTier tier, out int targetSlots, out double targetMaxUti, out int regionID, out string rarityPrefix, out int color)
        {
            targetSlots = 8;
            targetMaxUti = 85;
            regionID = 230;
            rarityPrefix = "Rare";
            color = 18;

            switch (tier)
            {
                case eRewardTier.RvRFinest: targetSlots = 11; targetMaxUti = 155; regionID = 230; rarityPrefix = "Mythical"; color = 26; break;
                case eRewardTier.PvPTier1: targetSlots = 10; targetMaxUti = 99; regionID = 230; rarityPrefix = "Legendary"; color = 22; break;
                case eRewardTier.PvPTier2: targetSlots = 8; targetMaxUti = 95; regionID = 233; rarityPrefix = "Epic"; color = 19; break;
                case eRewardTier.PvPTier3: targetSlots = 8; targetMaxUti = 85; regionID = 35; rarityPrefix = "Rare"; color = 18; break;
            }
        }

        private static void SpawnWithConfig(GamePlayer player, eRewardTier tier, GameNPC teleporter, int targetSlots, double targetMaxUti, int regionID, string rarityPrefix, int color)
        {
            GameObject refObj = teleporter != null ? (GameObject)teleporter : (GameObject)player;
            ushort baseHeading = refObj.Heading;

            ushort angle45 = 512;
            ushort angle25 = 284;

            ushort baseFacing = (ushort)((baseHeading + angle25) % 4096);
            ushort c1PlacementHeading = baseHeading;
            ushort c1FacingHeading = baseFacing;

            ushort c2PlacementHeading = (ushort)((baseHeading + angle45) % 4096);
            ushort c2FacingHeading = (ushort)((baseFacing + angle25) % 4096);

            ushort c3PlacementHeading = (ushort)((baseHeading - angle45 + 4096) % 4096);
            ushort c3FacingHeading = (ushort)((baseFacing - angle25 + 4096) % 4096);

            var c1 = CreateChest(refObj, c1PlacementHeading, c1FacingHeading, player.InternalID, tier, eRewardChestType.ScrollsAndBuffs, new LootGeneratorRewardScrolls(), "Chest of Magic Scrolls", targetSlots, targetMaxUti, regionID, rarityPrefix, color);
            var c2 = CreateChest(refObj, c2PlacementHeading, c2FacingHeading, player.InternalID, tier, eRewardChestType.ArmorsAndWeapons, new LootGeneratorRewardArmors(), "Chest of Armaments", targetSlots, targetMaxUti, regionID, rarityPrefix, color);
            var c3 = CreateChest(refObj, c3PlacementHeading, c3FacingHeading, player.InternalID, tier, eRewardChestType.Jewellery, new LootGeneratorRewardJewels(), "Chest of Magical Jewellery", targetSlots, targetMaxUti, regionID, rarityPrefix, color);

            c1.Siblings.Add(c2); c1.Siblings.Add(c3);
            c2.Siblings.Add(c1); c2.Siblings.Add(c3);
            c3.Siblings.Add(c1); c3.Siblings.Add(c2);

            c1.AddToWorld();
            c2.AddToWorld();
            c3.AddToWorld();

            log.Info($"[Reward System] Successfully spawned 3 {tier} chests for {player.Name} near {refObj.Name}.");
        }

        private static RewardChest CreateChest(GameObject refObj, ushort placementHeading, ushort facingHeading, string ownerId, eRewardTier tier, eRewardChestType type, ILootGenerator generator, string name, int targetSlots, double targetMaxUti, int regionID, string rarityPrefix, int color)
        {
            int distance = 160;
            double angleRadians = Angle.Heading(placementHeading).InRadians;

            int cx = refObj.Position.X - (int)Math.Round(Math.Sin(angleRadians) * distance);
            int cy = refObj.Position.Y + (int)Math.Round(Math.Cos(angleRadians) * distance);
            int cz = refObj.Position.Z;

            var chest = new RewardChest
            {
                Name = name,
                Model = 1596,
                CurrentRegionID = refObj.CurrentRegionID,
                Heading = facingHeading,
                Position = Position.Create(refObj.CurrentRegionID, cx, cy, cz, facingHeading),
                Realm = eRealm.None,
                Tier = tier,
                ChestType = type,
                OwnerID = ownerId,
                LootGenerators = new List<ILootGenerator> { generator },
                ItemChance = 100,

                ConfigTargetSlots = targetSlots,
                ConfigTargetMaxUti = targetMaxUti,
                ConfigRegionID = regionID,
                ConfigRarityPrefix = rarityPrefix,
                ConfigItemColor = color
            };

            return chest;
        }

        /// <summary>
        /// Clears out any leftover, un-picked chests from previous sessions.
        /// </summary>
        public static void ClearOldChests()
        {
            foreach (var region in WorldMgr.GetAllRegions())
            {
                var oldChests = region.Objects.OfType<RewardChest>().ToList();
                foreach (var chest in oldChests)
                {
                    chest.RemoveFromWorld();
                    chest.Delete();
                }
            }
        }
    }
}