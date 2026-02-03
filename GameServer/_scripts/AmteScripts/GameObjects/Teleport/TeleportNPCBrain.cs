using DOL.Events;
using DOL.GS;
using DOL.GS.Scripts;
using System.Linq;

namespace DOL.AI.Brain
{
    public class TeleportNPCBrain : AmteMobBrain
    {
        public override int ThinkInterval => 500;
        private uint m_previousTick = 0;

        public override void Think()
        {
            base.Think();
            
            if (Body is not TeleportNPC teleportNPC)
                return;

            if (teleportNPC.IsAreaPulseActive)
            {
                teleportNPC.HandleAreaPulseThink();
            }
            else
            {
                teleportNPC.JumpArea();
            }

            var currentTick = Body.CurrentRegion.GameTime;
            if (teleportNPC.HasHourConditions && teleportNPC.JumpPositions.Values.Any(j => j.Conditions.IsActiveAtTick(m_previousTick)) != teleportNPC.JumpPositions.Values.Any(j => j.Conditions.IsActiveAtTick(currentTick)))
            {
                foreach (var player in Body.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE, true).Cast<GamePlayer>())
                {
                    Body.RefreshEffects(player);
                }
            }
            
            m_previousTick = currentTick;
        }

        protected override int BrainTimerCallback(RegionTimer callingTimer)
        {
            if (!m_body.IsAlive || m_body.ObjectState != GameObject.eObjectState.Active)
            {
                //Stop the brain for dead or inactive bodies
                Stop();
                return 0;
            }

            Think();
            GameEventMgr.Notify(GameNPCEvent.OnAICallback, m_body);
            return ThinkInterval;
        }
    }
}
