using System;

namespace DOL.GS
{
    [Obsolete("Legacy shim. Packet handlers should use GameLoopPost.Run(...); everything else ECSGameTimerWrapperBase.")]
    public abstract class RegionAction : ECSGameTimerWrapperBase
    {
        protected readonly GameObject m_actionSource;
        private int m_repeatInterval;   // legacy GameTimer.Interval: 0 => one-shot

        protected RegionAction(GameObject actionSource) : base(actionSource)
        {
            m_actionSource = actionSource ?? throw new ArgumentNullException(nameof(actionSource));
        }

        /// <summary>Legacy repeat interval (0 = fire once). Hides ECSGameTimer.Interval on purpose:
        /// that one is overwritten by Start(1) and would make every packet action repeat every tick.</summary>
        public new int Interval { get => m_repeatInterval; set => m_repeatInterval = value; }

        protected sealed override int OnTick(ECSGameTimer timer)
        {
            OnTick();
            return IsAlive ? m_repeatInterval : 0;
        }

        public abstract void OnTick();
    }
}