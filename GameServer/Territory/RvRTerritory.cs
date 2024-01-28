using DOL.GS;
using DOL.GS.Keeps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DOL.Territory
{
    public class RvRTerritory
        : Territory
    {
        public RvRTerritory(IArea area, string areaId, Vector3 center, ushort regionId, ushort zoneId, GameNPC boss)
            : base(area, areaId, center, regionId, zoneId, null, boss, false)
        {
            //add new area to region
            //only in memory
            if (WorldMgr.Regions.ContainsKey(this.RegionId))
            {
                WorldMgr.Regions[this.RegionId].AddArea(this.Area);
            }
        }

        protected override void SaveOriginalGuilds()
        {
            if (this.Mobs != null)
            {
                this.Mobs.ForEach(m => this.SaveMobOriginalGuildname(m));
            }
        }

        public override void SaveIntoDatabase()
        {
            //In memory RvR
            //No save allowed
        }

        public void Reset()
        {
            this.Boss.RestoreOriginalGuildName();
            this.Boss.Realm = eRealm.None;
            foreach (var mob in this.Mobs)
            {
                mob.Realm = eRealm.None;
                if (this.OriginalGuilds.ContainsKey(mob.InternalID))
                {
                    mob.GuildName = this.OriginalGuilds[mob.InternalID];
                }
                else
                {
                    mob.GuildName = null;
                }
            }
            // reset keep
            AbstractGameKeep keep = GameServer.KeepManager.GetKeepCloseToSpot(RegionId, Boss.Position, 100000);
            keep.TempRealm = eRealm.None;
            keep.Reset(keep.TempRealm);
            // reset all doors
            foreach (GameKeepDoor door in keep.Doors.Values)
            {
                door.Reset((eRealm)6);
            }
            ClearPortal();
        }
    }
}