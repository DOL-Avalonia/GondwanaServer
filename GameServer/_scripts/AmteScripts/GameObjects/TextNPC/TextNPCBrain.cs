using DOL.Events;
using DOL.GS;
using DOL.GS.Scripts;
using System.Linq;

namespace DOL.AI.Brain
{
    public class TextNPCBrain : AmteMobBrain
    {
        public override int ThinkInterval
        {
            get { return 1000; }
        }

        private uint m_previousTick = 0;

        public override void Think()
        {
            base.Think();

            if (Body is not ITextNPC iTextNpc)
                return;
            
            if (!Body.InCombat)
                iTextNpc.SayRandomPhrase();

            var currentTick = Body.CurrentRegion.GameTime;

            var policy = iTextNpc.GetTextNPCPolicy();
            // Note: this will not work for per-player policies (iTextNpc.GetTextNPCPolicy(player))
            if (policy?.Condition != null && policy.Condition.IsActiveAtTick(m_previousTick) != policy.Condition.IsActiveAtTick(currentTick))
            {
                foreach (var player in Body.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE, true).Cast<GamePlayer>())
                {
                    player.TempProperties.removeProperty("QuestIndState_" + Body.ObjectID);
                    Body.RefreshEffects(player);
                }
            }
            
            m_previousTick = currentTick;
        }
    }
}
