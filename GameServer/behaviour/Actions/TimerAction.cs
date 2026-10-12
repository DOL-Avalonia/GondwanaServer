using System;
using System.Collections.Generic;
using System.Text;
using DOL.GS.PacketHandler;
using DOL.Events;
using DOL.GS.Behaviour.Attributes;
using DOL.GS.Behaviour;
using DOL.Database;

namespace DOL.GS.Behaviour.Actions
{

    [ActionAttribute(ActionType = eActionType.Timer)]
    public class TimerAction : AbstractAction<string, int>
    {
        public TimerAction(GameNPC defaultNPC, Object p, Object q)
            : base(defaultNPC, eActionType.Timer, p, q)
        {
        }

        public TimerAction(GameNPC defaultNPC, string timerID, int delay)
            : this(defaultNPC, (object)timerID, (object)delay) { }

        public override void Perform(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = BehaviourUtils.GuessGamePlayerFromNotify(e, sender, args);

            if (player == null) return;
            string timerId = P;
            new ECSGameTimer(player, _ => { player.Notify(GameLivingEvent.Timer, player, new TimerEventArgs(player, timerId)); return 0; }, Q);
        }
    }
}
