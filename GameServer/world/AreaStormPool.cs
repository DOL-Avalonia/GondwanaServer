using System.Collections.Generic;
using System.Threading;
using DOL.AI.Brain;
using DOL.GS.Geometry;

namespace DOL.GS
{
    /// Reusable storm NPCs for area effect loops (CoreRun tore, Biohazard radioactive areas...).
    /// Set MAX_POOLED = 0 to disable pooling without touching callers.
    public static class AreaStormPool
    {
        private const int MAX_POOLED = 128;
        private static readonly Stack<GameNPC> _pool = new();
        private static readonly Lock _lock = new();

        public static GameNPC Rent(Region region, Coordinate spot, byte level, byte size, int faction, bool npcImmune)
        {
            GameNPC storm;
            lock (_lock) storm = _pool.Count > 0 ? _pool.Pop() : null;
            storm ??= Create();
            storm.Level = level;
            storm.Size = size;
            storm.Faction = faction > 0 ? FactionMgr.GetFactionByID(faction) : null;

            if (npcImmune)
            {
                storm.TempProperties.setProperty("NPCImmunToStorm", true);
            }
            else
            {
                storm.TempProperties.removeProperty("NPCImmunToStorm");
            }

            storm.Position = Position.Create(region.ID, spot.X, spot.Y, spot.Z, 0);

            if (storm.AddToWorld())
                return storm;

            Return(storm);
            return null;
        }

        public static void Return(GameNPC storm)
        {
            if (storm.ObjectState == GameObject.eObjectState.Active)
                storm.RemoveFromWorld();

            lock (_lock) { if (_pool.Count < MAX_POOLED) { _pool.Push(storm); return; } }
            storm.Delete();
        }

        private static GameNPC Create()
        {
            GameNPC npc = new()
            {
                Model = 667,
                Name = "Storm",
                Flags = GameNPC.eFlags.CANTTARGET | GameNPC.eFlags.DONTSHOWNAME
            };

            npc.SetOwnBrain(new BlankBrain());
            return npc;
        }
    }
}