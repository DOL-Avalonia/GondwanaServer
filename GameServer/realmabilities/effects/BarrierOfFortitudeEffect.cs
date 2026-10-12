using DOL.Events;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;

namespace DOL.GS.Effects
{
    /// <summary>
    /// Effect handler for Barrier Of Fortitude
    /// </summary>
    public class BarrierOfFortitudeEffect : StaticEffect, IGameEffect
    {
        private const String m_delveString = "Grants the group a melee absorption bonus (Does not stack with Soldier's Barricade or Bedazzling Aura).";
        private GamePlayer m_player;
        private Int32 m_effectDuration;
        private ECSGameTimer m_expireTimer;
        private int m_value;

        /// <summary>
        /// Default constructor for AmelioratingMelodiesEffect
        /// </summary>
        public BarrierOfFortitudeEffect()
        {

        }

        /// <summary>
        /// Called when effect is to be started
        /// </summary>
        /// <param name="player">The player to start the effect for</param>
        /// <param name="duration">The effectduration in secounds</param>
        /// <param name="value">The percentage additional value for melee absorb</param>
        public void Start(GamePlayer player, int duration, int value)
        {
            m_player = player;
            m_effectDuration = duration;
            m_value = value;

            if (player.TempProperties.getProperty(RealmAbilities.BarrierOfFortitudeAbility.BofBaSb, false))
                return;

            StartTimers();

            GameEventMgr.AddHandler(m_player, GamePlayerEvent.Quit, new DOLEventHandler(PlayerLeftWorld));

            m_player.AbilityBonus[(int)eProperty.ArmorAbsorption] += m_value;

            m_player.EffectList.Add(this);
            player.TempProperties.setProperty(RealmAbilities.BarrierOfFortitudeAbility.BofBaSb, true);
        }

        /// <summary>
        /// Called when a player leaves the game
        /// </summary>
        /// <param name="e">The event which was raised</param>
        /// <param name="sender">Sender of the event</param>
        /// <param name="args">EventArgs associated with the event</param>
        private static void PlayerLeftWorld(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = (GamePlayer)sender;

            BarrierOfFortitudeEffect BoFEffect = player.EffectList.GetOfType<BarrierOfFortitudeEffect>();
            if (BoFEffect != null)
            {
                BoFEffect.Cancel(false);
            }
        }

        /// <summary>
        /// Called when effect is to be cancelled
        /// </summary>
        /// <param name="playerCancel">Whether or not effect is player cancelled</param>
        public override bool Cancel(bool playerCancel, bool force = false)
        {
            StopTimers();
            m_player.AbilityBonus[(int)eProperty.ArmorAbsorption] -= m_value;
            m_player.EffectList.Remove(this);
            GameEventMgr.RemoveHandler(m_player, GamePlayerEvent.Quit, new DOLEventHandler(PlayerLeftWorld));
            m_player.TempProperties.removeProperty(RealmAbilities.BarrierOfFortitudeAbility.BofBaSb);
            return true;
        }

        private void StartTimers()
        {
            StopTimers();
            m_expireTimer = new ECSGameTimer(m_player, ExpireCallback, m_effectDuration * 1000);
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
                return "Barrier of Fortitude";
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
                return 3015;
            }
        }

        /// <summary>
        /// Delve information
        /// </summary>
        public override IList<string> DelveInfo
        {
            get
            {
                var delveInfoList = new List<string>(8);
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
