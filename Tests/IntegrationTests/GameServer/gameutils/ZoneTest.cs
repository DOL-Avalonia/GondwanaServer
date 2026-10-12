using System;
using DOL.GS;
using NUnit.Framework;

namespace DOL.Integration.Server
{
    /// <summary>
    /// Unit tests for the Zone Class
    /// </summary>
    [TestFixture]
    public class ZoneTest : ServerTests
    {

        public ZoneTest()
        {
        }

        [Test]
        public void GetZCoordinateTest()
        {

        }

        [SetUp]
        public void InitGetRandomNpcTest()
        {
            var testRegionData = new RegionData();
            testRegionData.Id = 0;
            testRegionData.Ip = "127.0.0.1";
            testRegionData.IsFrontier = false;
            testRegionData.Name = "TEST";
            testRegionData.Description = "TEST";
            testRegionData.Port = 42069;
            testRegionData.WaterLevel = 0;
            testRegionData.DivingEnabled = false;
            testRegionData.HousingEnabled = false;
            testRegionData.Expansion = (int)eClientExpansion.None;
            testRegionData.Mobs = new DOL.Database.Mob[]{
                new DOL.Database.Mob() {},
            };

            var testZoneData = new ZoneData()
            {
                ZoneID = 0,
                RegionID = testRegionData.Id,
                OffX = 1,
                OffY = 1,
                Height = 1,
                Width = 1,
                Description = "TEST",
                DivingFlag = 0,
                WaterLevel = testRegionData.WaterLevel,
                IsLava = false,
            };

            // TODO(Blasnoc) these nullchecks are temporary fixes until I figure out the disparities between
            // the appveyor env and local.
            if (WorldMgr.GetRegion(testRegionData.Id) == null)
                WorldMgr.RegisterRegion(testRegionData);
            if (WorldMgr.GetZone(testZoneData.ZoneID) == null)
                WorldMgr.RegisterZone(testZoneData, 0, 0, "TEST", 1, 1, 1, 1, (byte)eRealm.None, true, true, 1.0f, false);
        }

        [Test]
        public void GetRandomNPCTest()
        {
            Zone zone = WorldMgr.GetZone(0);
            Assert.IsNotNull(zone);

            StartWatch();
            GameNPC npc = zone.GetRandomNPC(eRealm.None, 5, 7);
            if (npc != null)
            {
                Console.WriteLine("Found NPC from Realm None in " + zone.ZoneRegion.Description + "/" + zone.Description + ":" + npc.Name + " level:" + npc.Level);
                if (npc.Level < 5 || npc.Level > 7)
                    Assert.Fail("NPC Level out of defined range");

                if (npc.Realm != eRealm.None)
                    Assert.Fail("NPC wrong Realm");
            }
            else
            {
                Console.WriteLine("nothing found in " + zone.ZoneRegion.Description + "/" + zone.Description);
            }
            StopWatch();

            StartWatch();
            npc = zone.GetRandomNPC(eRealm.Albion);
            if (npc != null)
            {
                Console.WriteLine("Found Albion NPC in " + zone.ZoneRegion.Description + "/" + zone.Description + ":" + npc.Name);

                if (npc.Realm != eRealm.Albion)
                    Assert.Fail("NPC wrong Realm");
            }
            else
            {
                Console.WriteLine("nothing found in " + zone.ZoneRegion.Description + "/" + zone.Description);
            }
            StopWatch();
        }
    }
}
