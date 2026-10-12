using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.PropertyCalc;
using DOL.GS.SkillHandler;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;

namespace DOL.GS.Effects
{
    /// <summary>
    /// Effect handler for Juggernaut
    /// </summary>
    public class JuggernautEffect : StaticEffect, IGameEffect
    {
        private const String m_delveString = "Increases the effective level of the pet by the listed number (capped at level 70).";
        private GameNPC m_living;
        private Int32 m_effectDuration;
        private ECSGameTimer m_expireTimer;
        private byte m_value;
        private int m_growSize = 15;
        public const int JUGGERNAUT_CAP_EFFECT = 70;


        /// <summary>
        /// Default constructor for JuggernautEffect
        /// </summary>
        public JuggernautEffect()
        {

        }

        /// <summary>
        /// Called when effect is to be started
        /// </summary>
        /// <param name="player">The player to start the effect for</param>
        /// <param name="duration">The effectduration in secounds</param>
        /// <param name="value">The increment of effective level</param>
        public void Start(GameLiving living, int duration, byte value)
        {
            if (living.ControlledBrain == null)
                return;
            m_living = living.ControlledBrain.Body;
            m_effectDuration = duration;
            m_value = value;


            StartTimers();

            m_living.Size += (byte)m_growSize;
            m_living.Level = (byte)Math.Min(m_living.Level + m_value, JUGGERNAUT_CAP_EFFECT);
            m_living.EffectList.Add(this);

        }

        /// <summary>
        /// Called when effect is to be cancelled
        /// </summary>
        /// <param name="playerCancel">Whether or not effect is player cancelled</param>
        public override bool Cancel(bool playerCancel, bool force = false)
        {
            StopTimers();
            m_living.Size -= (byte)m_growSize;
            m_living.Level -= m_value;
            m_living.EffectList.Remove(this);
            return true;
        }

        private void StartTimers()
        {
            StopTimers();
            m_expireTimer = new ECSGameTimer( m_living, ExpireCallback, m_effectDuration * 1000);
        }

        private void StopTimers()
        {
            m_expireTimer?.Stop();
            m_expireTimer = null;
        }

        private int ExpireCallback(ECSGameTimer timer)
        {
            Cancel(false);
            return 0;
        }

        /// <summary>
        /// Name of the effect
        /// </summary>
        public override string Name
        {
            get
            {
                return "Juggernaut";
            }
        }

        /// <summary>
        /// Remaining time of the effect in milliseconds
        /// </summary>
        public override Int32 RemainingTime
        {
            get
            {
                ECSGameTimer timer = m_expireTimer;
                return timer == null || !timer.IsAlive ? 0 : timer.TimeUntilElapsed;
            }
        }

        /// <summary>
        /// Icon ID
        /// </summary>
        public override UInt16 Icon
        {
            get
            {
                return 3030;
            }
        }

        /// <summary>
        /// Delve information
        /// </summary>
        public override IList<string> DelveInfo
        {
            get
            {
                var delveInfoList = new List<string>();
                delveInfoList.Add(m_delveString);
                delveInfoList.Add(" ");
                delveInfoList.Add("Value: " + m_value + "%");

                int seconds = (int)(RemainingTime / 1000);
                if (seconds > 0)
                {
                    delveInfoList.Add(" ");
                    delveInfoList.Add("- " + seconds + " seconds remaining.");
                }

                return delveInfoList;
            }
        }
    }
}