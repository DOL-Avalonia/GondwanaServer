using System;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using System.Threading;

namespace DOL.GS.Effects
{
    /// <summary>
    /// Spell Effect assists SpellHandler with duration spells with immunity
    /// </summary>
    public class GameSpellAndImmunityEffect : GameSpellEffect
    {
        /// <summary>
        /// The amount of times this effect started
        /// </summary>
        protected int m_startedCount;

        /// <summary>
        /// Creates a new game spell effect
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="duration"></param>
        /// <param name="pulseFreq"></param>
        public GameSpellAndImmunityEffect(ISpellHandler handler, int duration, int pulseFreq) : this(handler, duration, pulseFreq, 1)
        {
        }

        /// <summary>
        /// Creates a new game spell effect
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="duration"></param>
        /// <param name="pulseFreq"></param>
        /// <param name="effectiveness"></param>
        public GameSpellAndImmunityEffect(ISpellHandler handler, int duration, int pulseFreq, double effectiveness) : base(handler, duration, pulseFreq, effectiveness)
        {
            m_startedCount = 0;
        }

        /// <summary>
        /// Starts the timers for this effect
        /// </summary>
        protected override void StartDurationTimer()
        {
            // Duration => 0 = endless until explicit stop
            if (Duration == 0)
                return;
            
            int duration = Duration;
            if (!IsExpired)
            {
                int startcount = Interlocked.Increment(ref m_startedCount) - 1;
                if (startcount > 0) duration = Math.Max(1, duration / Math.Min(20, startcount * 2));
            }
            long timeLeft = m_startedTick + duration - GameLoop.GameLoopTime;
            if (m_startedTick != 0 && timeLeft > 0)
            {
                m_effectTimer ??= new ECSGameTimer(m_owner, ExpiredCallback);
                m_effectTimer.Start((int)timeLeft);
            }
        }

        /// <summary>
        /// Gets the amount of times this effect started
        /// </summary>
        public int StartedCount
        {
            get { return m_startedCount; }
        }
    }
}
